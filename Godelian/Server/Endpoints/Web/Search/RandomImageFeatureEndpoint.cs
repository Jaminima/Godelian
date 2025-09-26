using DnsClient.Protocol.Options;
using Godelian.Models;
using Godelian.Networking.DTOs;
using MongoDB.Driver;
using MongoDB.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Web.Search
{
    internal static class RandomImageFeatureEndpoint
    {
        // Cache for random image features to reduce DB sampling overhead on repeated calls
        private static readonly ConcurrentQueue<FeatureDTO> cachedRandomImageFeatures = new();
        private static readonly int RandomCacheSize = 20;
        private static bool isPrefetching = false;
        private static readonly object prefetchLock = new();

        private static async Task PrefetchRandomImageFeaturesAsync()
        {
            lock (prefetchLock)
            {
                if (isPrefetching) return;
                isPrefetching = true;
            }

            try
            {
                int need = RandomCacheSize - cachedRandomImageFeatures.Count;
                if (need <= 0) return;

                // Sample multiple image features at once
                List<Feature> docs = await DB.Collection<Feature>()
                                   .Aggregate()
                                   .Match(f => f.Type == FeatureType.Base64)
                                   .Sample(need)
                                   .ToListAsync();

                List<string> hostRecordIds = docs.Select(d => d.HostRecordID).ToList();

                List<HostRecordModel> hostRecords = await DB.Find<HostRecordModel>()
                                                        .Match(hr => hostRecordIds.Contains(hr.ID))
                                                        .ExecuteAsync();

                for (int i=0;i<docs.Count;i++)
                {
                    Feature f = docs[i];
                    HostRecordModel hostRecord = hostRecords.First(hr => hr.ID == f.HostRecordID);

                    cachedRandomImageFeatures.Enqueue(new FeatureDTO
                    {
                        ID = f.ID,
                        // when serving base64 image features, place the blob into Base64Content and leave Content null
                        Content = null,
                        Base64Content = f.Base64Content,
                        Type = f.Type,
                        Elaborated = f.Elaborated,
                        ParentFeature = null,
                        HostRecord = new HostRecordModelDTO
                        {
                            ID = hostRecord.ID,
                            IPIndex = hostRecord.IPIndex,
                            Iteration = hostRecord.Iteration,
                            IPAddress = hostRecord.IPAddress,
                            Hostname = hostRecord.Hostname,
                            FoundByClientId = hostRecord.FoundByClientId,
                            FoundAt = hostRecord.FoundAt,
                            FeaturesElaborated = hostRecord.FeaturesElaborated,
                            HostRequestMethod = hostRecord.HostRequestMethod,
                        }
                    });
                }
            }
            catch
            {
                // Ignore prefetch errors; fallback will query directly
            }
            finally
            {
                lock (prefetchLock)
                {
                    isPrefetching = false;
                }
            }
        }

        public static async Task<ServerResponse<FeatureDTO>> GetRandomImageFeature(ClientRequest<object> clientRequest)
        {
            // Try to serve from cache first
            if (cachedRandomImageFeatures.TryDequeue(out FeatureDTO? cached))
            {
                // Fire-and-forget ensure cache gets refilled asynchronously
                _ = PrefetchRandomImageFeaturesAsync();

                return new ServerResponse<FeatureDTO>
                {
                    Success = true,
                    Data = cached
                };
            }

            // If cache empty, attempt to prefill (but don't wait long) and fall back to direct DB sample
            _ = PrefetchRandomImageFeaturesAsync();

            Feature? feature = await DB.Collection<Feature>()
                                .Aggregate()
                                .Match(f => f.Type == FeatureType.Base64)
                                .Sample(1)
                                .FirstOrDefaultAsync();

            if (feature is null)
            {
                return new ServerResponse<FeatureDTO>
                {
                    Success = false,
                    Message = "No image features found"
                };
            }

            HostRecordModel hostRecord = await DB.Find<HostRecordModel>().MatchID(feature.HostRecordID).ExecuteSingleAsync();

            return new ServerResponse<FeatureDTO>
            {
                Success = true,
                Data = new FeatureDTO
                {
                    ID = feature.ID,
                    // Base64 payload goes into Base64Content to avoid indexing
                    Content = null,
                    Base64Content = feature.Base64Content,
                    Type = feature.Type,
                    Elaborated = feature.Elaborated,
                    ParentFeature = null,
                    HostRecord = new HostRecordModelDTO
                    {
                            ID = hostRecord.ID,
                            IPIndex = hostRecord.IPIndex,
                            Iteration = hostRecord.Iteration,
                            IPAddress = hostRecord.IPAddress,
                            Hostname = hostRecord.Hostname,
                            FoundByClientId = hostRecord.FoundByClientId,
                            FoundAt = hostRecord.FoundAt,
                            FeaturesElaborated = hostRecord.FeaturesElaborated,
                            HostRequestMethod = hostRecord.HostRequestMethod,
                    }
                }
            };
        }
    }
}

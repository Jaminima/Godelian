using Godelian.Models;
using Godelian.Networking.DTOs;
using Godelian.Server.Endpoints.Client.Connection.DTOs;
using Godelian.Server.Endpoints.Client.FeatureRange.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Client.FeatureRange
{
    internal static class FeatureRangeEndpoints
    {
        // In-memory cache to avoid frequent costly $sample on a huge collection
        private static readonly ConcurrentQueue<FeatureDTO> cachedFeatureWorkItems = new();
        private static readonly object prefetchLock = new();
        private static bool isPrefetching = false;

        // Tune these as needed
        private const int ServeBatchSize = 50;           // how many items to return per client request
        private const int PrefetchTargetSize = 5000;     // desired items in memory cache
        private const int PrefetchLowWatermark = 1000;    // when cache falls below this, trigger prefetch

        private static async Task PrefetchFeatureWorkAsync()
        {
            lock (prefetchLock)
            {
                if (isPrefetching) return;
                isPrefetching = true;
            }

            try
            {
                int need = PrefetchTargetSize - cachedFeatureWorkItems.Count;
                if (need <= 0) return;

                FeatureType targetType = Random.Shared.Next(0,2) == 0 ? FeatureType.Image : FeatureType.Link;

                List<Feature> docs = await DB.Collection<Feature>()
                                       .Aggregate()
                                       .Match(f => f.Elaborated == false)
                                       .Match(f => f.Type == targetType)
                                       .Limit(need)
                                       .ToListAsync();

                if (docs.Count == 0) return;

                List<string> hostRecordIds = docs.Select(d => d.HostRecordID).Distinct().ToList();

                List<HostRecordModel> hostRecords = await DB.Find<HostRecordModel>()
                                                        .Match(hr => hostRecordIds.Contains(hr.ID))
                                                        .ExecuteAsync();

                // Pre-map host records for faster lookup
                Dictionary<string, HostRecordModelDTO> hostDtoById = hostRecords.ToDictionary(
                    hr => hr.ID,
                    hr => new HostRecordModelDTO
                    {
                        ID = hr.ID,
                        IPIndex = hr.IPIndex,
                        Iteration = hr.Iteration,
                        IPAddress = hr.IPAddress,
                        Hostname = hr.Hostname,
                        FoundByClientId = hr.FoundByClientId,
                        FoundAt = hr.FoundAt,
                        FeaturesElaborated = hr.FeaturesElaborated,
                        HostRequestMethod = hr.HostRequestMethod,
                    });

                foreach (Feature f in docs)
                {
                    if (!hostDtoById.TryGetValue(f.HostRecordID, out HostRecordModelDTO? hostDto))
                        continue;

                    cachedFeatureWorkItems.Enqueue(new FeatureDTO
                    {
                        ID = f.ID,
                        Content = f.Content,
                        Base64Content = null,
                        Type = f.Type,
                        Elaborated = f.Elaborated,
                        ParentFeature = null,
                        HostRecord = hostDto
                    });
                }
            }
            catch
            {
                // Ignore prefetch errors; callers will fall back
            }
            finally
            {
                lock (prefetchLock)
                {
                    isPrefetching = false;
                }
            }
        }

        public static async Task<ServerResponse<FeatureRangeCollection>> GetFeatureRange(ClientRequest<object> clientRequest)
        {
            // Try to serve from in-memory cache first
            List<FeatureDTO> items = new();
            while (items.Count < ServeBatchSize && cachedFeatureWorkItems.TryDequeue(out FeatureDTO? item))
            {
                items.Add(item);
            }

            // Trigger background prefetch when we dip under the low watermark
            if (cachedFeatureWorkItems.Count < PrefetchLowWatermark)
            {
                _ = PrefetchFeatureWorkAsync();
            }

            // If cache empty, fall back to direct DB fetch (legacy path)
            if (items.Count == 0)
            {
                List<Feature> records = await DB.Collection<Feature>()
                                         .Aggregate()
                                         .Match(f => f.Elaborated == false)
                                         .Match(f => f.Type == FeatureType.Image || f.Type == FeatureType.Link)
                                         .Limit(ServeBatchSize)
                                         .ToListAsync();

                List<string> hostRecordIds = records.Select(r => r.HostRecordID).ToList();

                List<HostRecordModel> hostRecords = await DB.Find<HostRecordModel>()
                                                        .Match(hr => hostRecordIds.Contains(hr.ID))
                                                        .Limit(ServeBatchSize)
                                                        .ExecuteAsync();

                List<HostRecordModelDTO> _hostRecords = hostRecords.Select(x => new HostRecordModelDTO
                {
                    ID = x.ID,
                    IPIndex = x.IPIndex,
                    Iteration = x.Iteration,
                    IPAddress = x.IPAddress,
                    Hostname = x.Hostname,
                    FoundByClientId = x.FoundByClientId,
                    FoundAt = x.FoundAt,
                    FeaturesElaborated = x.FeaturesElaborated,
                    HostRequestMethod = x.HostRequestMethod,
                }).ToList();

                items = records.Select(x => new FeatureDTO
                {
                    ID = x.ID,
                    Content = x.Content,
                    Base64Content = null,
                    Type = x.Type,
                    HostRecord = _hostRecords.First(y => y.ID == x.HostRecordID)
                }).ToList();
            }

            Console.WriteLine($"Client '{clientRequest.ClientNickname}' assigned {items.Count} records to analyze features");

            return new ServerResponse<FeatureRangeCollection>()
            {
                Data = new FeatureRangeCollection()
                {
                    featureRecords = items.ToArray()
                },
                Message = "Fetched feature range records",
                Success = true
            };
        }

        public static async Task<ServerResponse> SubmitFeatureRanges(ClientRequest<FeatureRangeCollection> clientRequest)
        {

            List<string> parentFeatureIDs = clientRequest.Data!.featureRecords.Select(x => x.ParentFeature!.ID!).Distinct().ToList();
            List<Feature> parentFeatures = await DB.Find<Feature>().ManyAsync(x => parentFeatureIDs.Contains(x.ID));

            List<Feature> newFeatures = new List<Feature>();

            foreach (FeatureDTO feature in clientRequest.Data!.featureRecords)
            {
                Feature parentFeature = parentFeatures.First(pf => pf.ID == feature.ParentFeature!.ID);

                Feature record = new Feature
                {
                    Content = feature.Content,
                    Base64Content = feature.Base64Content,
                    Type = feature.Type,
                    HostRecordID = parentFeature.HostRecordID,
                    ParentFeatureID = parentFeature.ID
                };

                newFeatures.Add(record);
            }

            foreach (Feature feature in parentFeatures)
            {
                feature.Elaborated = true;
            }

            List<Feature> allToSave = parentFeatures.Concat(newFeatures).ToList();
            if (allToSave.Count > 0)
                await DB.SaveAsync(allToSave);

            Console.WriteLine($"Client '{clientRequest.ClientNickname}' submitted features for {clientRequest.Data.featureRecords.Length} records");

            return new ServerResponse
            {
                Success = true,
                Message = "Feature ranges submitted successfully."
            };
        }
    }
}

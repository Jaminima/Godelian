using Godelian.Models;
using Godelian.Networking.DTOs;
using MongoDB.Driver;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Godelian.Server.Endpoints.Web.Search.DTOs;

namespace Godelian.Server.Endpoints.Web.Search
{
    internal static class RandomHostRecordEndpoint
    {
        // Cache for random records to reduce DB sampling overhead on repeated calls
        private static readonly ConcurrentQueue<HostRecordModelDTO> cachedRandomRecords = new();
        private static readonly int RandomCacheSize = 20;
        private static bool isPrefetching = false;
        private static readonly object prefetchLock = new();

        private static async Task PrefetchRandomRecordsAsync()
        {
            // Ensure only one prefetch runs at a time
            lock (prefetchLock)
            {
                if (isPrefetching) return;
                isPrefetching = true;
            }

            try
            {
                int need = RandomCacheSize - cachedRandomRecords.Count;
                if (need <= 0) return;

                // Use MongoDB aggregation sample to retrieve multiple random documents at once
                List<HostRecordModel> docs = await DB.Collection<HostRecordModel>()
                                   .Aggregate()
                                   .Sample(need)
                                   .ToListAsync();

                List<string> docIds = docs.Select(d => d.ID).ToList();

                List<HeaderRecord> headerRecords = await DB.Collection<HeaderRecord>()
                    .Aggregate()
                    .Match(x => docIds.Contains(x.HostRecordID))
                    .ToListAsync();

                List<Feature> docFeatures = await DB.Collection<Feature>().Aggregate().Match(x=>docIds.Contains(x.HostRecordID)).ToListAsync();

                foreach (HostRecordModel? record in docs)
                {
                    cachedRandomRecords.Enqueue(new HostRecordModelDTO
                    {
                        ID = record.ID,
                        IPAddress = record.IPAddress,
                        Hostname = record.Hostname,
                        FoundAt = record.FoundAt,
                        FoundByClientId = record.FoundByClientId,
                        HostRequestMethod = record.HostRequestMethod,
                        Iteration = record.Iteration,
                        FeaturesElaborated = record.FeaturesElaborated,
                        HeaderRecords = headerRecords
                                .Where(f => f.HostRecordID == record.ID)
                                .Select(headerRecords => new HeaderRecordDTO
                                {
                                    ID = headerRecords.ID,
                                    Name = headerRecords.Name,
                                    Value = headerRecords.Value,
                                    HostRecordID = headerRecords.HostRecordID
                                }).ToList(),
                        Features = docFeatures
                                .Where(f => f.HostRecordID == record.ID)
                                .Select(f => new FeatureDTO
                                {
                                    ID = f.ID,
                                    Content = f.Content,
                                    Type = f.Type,
                                    Elaborated = f.Elaborated,
                                    ParentFeature = null,
                                    HostRecord = null
                                }).ToList()
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

        public static async Task<ServerResponse<HostRecordModelDTO>> GetRandomRecord(ClientRequest<object> clientRequest)
        {
            // Try to serve from cache first
            if (cachedRandomRecords.TryDequeue(out HostRecordModelDTO? cached))
            {
                // Fire-and-forget ensure cache gets refilled asynchronously
                _ = PrefetchRandomRecordsAsync();

                return new ServerResponse<HostRecordModelDTO>
                {
                    Success = true,
                    Data = cached
                };
            }

            // If cache empty, attempt to prefill (but don't wait long) and fall back to direct DB sample
            _ = PrefetchRandomRecordsAsync();

            HostRecordModel? record = await DB.Collection<HostRecordModel>()
                                 .Aggregate()
                                 .Sample(1)
                                 .FirstOrDefaultAsync();

            if (record is null)
            {
                return new ServerResponse<HostRecordModelDTO>
                {
                    Success = false,
                    Message = "No records found"
                };
            }

            List<HeaderRecord> headerRecords = await DB.Collection<HeaderRecord>()
                .Aggregate()
                .Match(x => record.ID == x.HostRecordID)
                .ToListAsync();

            List<Feature> docFeatures = await DB.Collection<Feature>().Aggregate().Match(x => record.ID == x.HostRecordID).ToListAsync();

            return new ServerResponse<HostRecordModelDTO>
            {
                Success = true,
                Data = new HostRecordModelDTO
                {
                    ID = record.ID,
                    IPAddress = record.IPAddress,
                    Hostname = record.Hostname,
                    FoundAt = record.FoundAt,
                    FoundByClientId = record.FoundByClientId,
                    HostRequestMethod = record.HostRequestMethod,
                    Iteration = record.Iteration,
                    FeaturesElaborated = record.FeaturesElaborated,
                    HeaderRecords = headerRecords.Select(headerRecords => new HeaderRecordDTO
                    {
                        ID = headerRecords.ID,
                        Name = headerRecords.Name,
                        Value = headerRecords.Value,
                        HostRecordID = headerRecords.HostRecordID
                    }).ToList(),
                    Features = docFeatures
                            .Select(f => new FeatureDTO
                            {
                                ID = f.ID,
                                Content = f.Content,
                                Type = f.Type,
                                Elaborated = f.Elaborated,
                                ParentFeature = null,
                                HostRecord = null
                            }).ToList()
                }
            };
        }
    }
}

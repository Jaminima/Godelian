using Godelian.Models;
using Godelian.Networking.DTOs;
using Godelian.Services;
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
    internal static class SearchEndpoints
    {
        public static async Task<ServerResponse<SearchResults>> SearchRecords(ClientRequest<SearchQuery> clientRequest)
        {
            string query  = clientRequest.Data?.Query?.ToLower() ?? "";

            List<HostRecordModel> indexes = await DB.Find<HostRecordModel>().Match(x => x.Features.Any(y=>y.Content.ToLower().Contains(query)))
                .Sort(x => x.ID, Order.Descending)
                .Limit(10)
                .ExecuteAsync();

            indexes.ForEach(x => x.Features = x.Features.Where(y => y.Content.ToLower().Contains(query)).ToList());

            return new ServerResponse<SearchResults>
            {
                Success = true,
                Data = new SearchResults { hostRecords = indexes.ToArray() }
            };
        }

        // Cache for random records to reduce DB sampling overhead on repeated calls
        private static readonly ConcurrentQueue<HostRecordModel> cachedRandomRecords = new();
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
                var docs = await DB.Collection<HostRecordModel>()
                                   .Aggregate()
                                   .Sample(need)
                                   .ToListAsync();

                foreach (var d in docs)
                {
                    cachedRandomRecords.Enqueue(d);
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

        public static async Task<ServerResponse<HostRecordModel>> GetRandomRecord(ClientRequest<object> clientRequest)
        {
            // Try to serve from cache first
            if (cachedRandomRecords.TryDequeue(out var cached))
            {
                // Fire-and-forget ensure cache gets refilled asynchronously
                _ = PrefetchRandomRecordsAsync();

                return new ServerResponse<HostRecordModel>
                {
                    Success = true,
                    Data = cached
                };
            }

            // If cache empty, attempt to prefill (but don't wait long) and fall back to direct DB sample
            _ = PrefetchRandomRecordsAsync();

            var record = await DB.Collection<HostRecordModel>()
                                 .Aggregate()
                                 .Sample(1)
                                 .FirstOrDefaultAsync();

            if (record is null)
            {
                return new ServerResponse<HostRecordModel>
                {
                    Success = false,
                    Message = "No records found"
                };
            }

            return new ServerResponse<HostRecordModel>
            {
                Success = true,
                Data = record
            };
        }
    }
}

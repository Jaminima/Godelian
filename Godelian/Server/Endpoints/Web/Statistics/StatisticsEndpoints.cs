using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking.DTOs;
using Godelian.Server;
using Godelian.Server.Endpoints.Web.Statistics.DTOs;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Web.Statistics
{
    internal static class StatisticsEndpoints
    {
        public static async Task<ServerResponse<ProgressStats>> ProgressStatistics(ClientRequest<object> clientRequest)
        {
            ulong totalRecords = (ulong)await DB.Collection<HostRecordModel>().EstimatedDocumentCountAsync();

            ProgressStats progressStats = new ProgressStats()
            {
                CurrentIPIndex = ProgressEstimatorService.CurrentIndex,
                CurrentIPAddress = IPAddressEnumerator.GetIndexAsIP(ProgressEstimatorService.CurrentIndex),
                PercentageComplete = ProgressEstimatorService.GetPercentageProgress(),
                EstimatedTimeRemaining = ProgressEstimatorService.EstimateTimeRemaining().ToString(@"d' Days 'hh\:mm\:ss"),
                FoundHosts = totalRecords
            };

            return new ServerResponse<ProgressStats>
            {
                Success = true,
                Data = progressStats
            };
        }

        public static async Task<ServerResponse<RecentlyActiveClients>> GetRecentlyActiveClients(ClientRequest<object> clientRequest)
        {
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-10);
            List<ClientModel> recentClients = await DB.Find<ClientModel>()
                                                     .Match(c => c.LastActiveAt >= cutoff)
                                                     .Sort(c => c.LastActiveAt, Order.Descending)
                                                     .ExecuteAsync();

            return new ServerResponse<RecentlyActiveClients>
            {
                Success = true,
                Data = new RecentlyActiveClients
                {
                    Clients = recentClients.ToArray()
                }
            };
        }

        public static async Task<ServerResponse<IPDistributionStatsResponse>> GetIPDistributionStats(ClientRequest<IPDistributionStats> clientRequest)
        {
            List<IPBatch> batches = await DB.Find<IPBatch>().Sort(x=>x.ID,Order.Ascending).ExecuteAsync();

            int requestedBuckets = clientRequest.Data?.NumBuckets ?? 128;
            if (requestedBuckets <= 0) requestedBuckets = 1;

            IPDistributionBucket[] buckets;

            if (batches.Count == 0)
            {
                buckets = Array.Empty<IPDistributionBucket>();
            }
            else if (batches.Count <= requestedBuckets)
            {
                buckets = batches.Select(b => new IPDistributionBucket
                {
                    StartIP = b.StartIP,
                    EndIP = b.EndIP,
                    NumIPs = b.FoundIps ?? 0
                }).ToArray();
            }
            else
            {
                int chunkSize = (int)Math.Ceiling(batches.Count / (double)requestedBuckets);
                List<IPDistributionBucket> compressed = new List<IPDistributionBucket>(requestedBuckets);

                for (int i = 0; i < requestedBuckets; i++)
                {
                    int start = i * chunkSize;
                    if (start >= batches.Count) break;
                    int endExclusive = Math.Min(start + chunkSize, batches.Count);

                    List<IPBatch> group = batches.Skip(start).Take(endExclusive - start).ToList();
                    if (group.Count == 0) break;

                    compressed.Add(new IPDistributionBucket
                    {
                        StartIP = group.First().StartIP,
                        EndIP = group.Last().EndIP,
                        NumIPs = group.Sum(b => b.FoundIps ?? 0)
                    });
                }

                buckets = compressed.ToArray();
            }

            return new ServerResponse<IPDistributionStatsResponse>
            {
                Success = true,
                Data = new IPDistributionStatsResponse
                {
                    Buckets = buckets
                }
            };
        }
    }
}

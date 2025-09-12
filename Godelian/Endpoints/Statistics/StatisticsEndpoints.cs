using Godelian.Endpoints.IPAddreessing.DTOs;
using Godelian.Endpoints.Statistics.DTOs;
using Godelian.Helpers;
using Godelian.Models;
using Godelian.Networking.DTOs;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Godelian.Endpoints.Statistics
{
    internal static class StatisticsEndpoints
    {
        public static async Task<ServerResponse<ProgressStats>> ProgressStatistics(ClientRequest<object> clientRequest)
        {
            ulong totalRecords = (ulong)await DB.Collection<HostRecordModel>().EstimatedDocumentCountAsync();

            ProgressStats progressStats = new ProgressStats()
            {
                CurrentIPIndex = ProgressEstimator.CurrentIndex,
                CurrentIPAddress = IPAddressEnumerator.GetIndexAsIP(ProgressEstimator.CurrentIndex),
                PercentageComplete = ProgressEstimator.GetPercentageProgress(),
                EstimatedTimeRemaining = ProgressEstimator.EstimateTimeRemaining().ToString(@"d' Days 'hh\:mm\:ss"),
                FoundHosts = totalRecords
            };

            return new ServerResponse<ProgressStats>
            {
                Success = true,
                Data = progressStats
            };
        }

        public static async Task<ServerResponse<IPIndexStats>> GetAllIPIndexes(ClientRequest<object> clientRequest)
        {
            List<HostRecordModel> indexes = await DB.Find<HostRecordModel>()
                                          .Sort(x => x.ID, Order.Ascending)
                                          .Project(x=>new HostRecordModel { ID = x.ID, IPIndex = x.IPIndex, IPAddress = x.IPAddress, Hostname = x.Hostname, FoundByClientId = x.FoundByClientId })
                                          .ExecuteAsync();

            return new ServerResponse<IPIndexStats>
            {
                Success = true,
                Data = new IPIndexStats { hostRecords = indexes.ToArray() }
            };
        }

        public static async Task<ServerResponse<RecentlyActiveClients>> GetRecentlyActiveClients(ClientRequest<object> clientRequest)
        {
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-5);
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
            var distributionResults = await DB.Find<IPBatch>().Sort(x=>x.ID,Order.Ascending).ExecuteAsync();

            int[] ipDistribution = distributionResults.Select(b => b.FoundIps ?? 0).ToArray();

            //Compress to 256 length array

            int size = clientRequest.Data?.NumBuckets ?? 128;

            if (ipDistribution.Length > size)
            {
                int factor = ipDistribution.Length / size;
                int[] compressed = new int[size];
                for (int i = 0; i < size; i++)
                {
                    compressed[i] = ipDistribution.Skip(i * factor).Take(factor).Sum();
                }
                ipDistribution = compressed;
            }


            return new ServerResponse<IPDistributionStatsResponse>
            {
                Success = true,
                Data = new IPDistributionStatsResponse
                {
                    NumIPs = ipDistribution
                }
            };
        }
    }
}

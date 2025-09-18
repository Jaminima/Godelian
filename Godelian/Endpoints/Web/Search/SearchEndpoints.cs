using Godelian.Endpoints.Web.Search.DTOs;
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

namespace Godelian.Endpoints.Web.Search
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

        public static async Task<ServerResponse<HostRecordModel>> GetRandomRecord(ClientRequest<object> clientRequest)
        {
            IterationTracker iteration = await IterationService.GetCurrentIteration();
            int currentIteration = iteration.Iteration;

            var record = await DB.Collection<HostRecordModel>()
                                 .Aggregate()
                                 .Match(x=>x.Iteration == currentIteration)
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

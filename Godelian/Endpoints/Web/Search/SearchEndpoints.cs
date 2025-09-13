using Godelian.Endpoints.Web.Search.DTOs;
using Godelian.Models;
using Godelian.Networking.DTOs;
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
    }
}

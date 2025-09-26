using Godelian.Models;
using Godelian.Networking.DTOs;
using Godelian.Server.Endpoints.Web.Statistics.DTOs;
using MongoDB.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Godelian.Server.Endpoints.Web.Statistics
{
    internal static class HeaderStatisticsEndpoints
    {

        // Returns most common header names across all records
        public static async Task<ServerResponse<HeaderStatsResponse>> GetTopHeaderNames(ClientRequest<HeaderStatsRequest> clientRequest)
        {
            int topN = clientRequest.Data?.TopN ?? 20;
            if (topN <= 0) topN = 1;

            // Load all header records and aggregate in-memory
            List<HeaderRecord> headers = await DB.Find<HeaderRecord>().ExecuteAsync();

            var groups = headers
                .GroupBy<HeaderRecord, string>(h => h.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new HeaderNameCount { Name = g.Key, Count = g.LongCount() })
                .OrderByDescending(x => x.Count)
                .Take(topN)
                .ToArray();

            return new ServerResponse<HeaderStatsResponse>
            {
                Success = true,
                Data = new HeaderStatsResponse
                {
                    TopHeaderNames = groups
                }
            };
        }

        // Returns most common values for a specific header name (e.g. Server)
        public static async Task<ServerResponse<HeaderValueStatsResponse>> GetTopHeaderValues(ClientRequest<HeaderValueStatsRequest> clientRequest)
        {
            string headerName = clientRequest.Data?.HeaderName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(headerName))
            {
                return new ServerResponse<HeaderValueStatsResponse>
                {
                    Success = false,
                    Message = "HeaderName is required in request data"
                };
            }

            int topN = clientRequest.Data?.TopN ?? 20;
            if (topN <= 0) topN = 1;

            // Fetch header records matching the name (case-insensitive by filtering in-memory)
            List<HeaderRecord> headers = await DB.Find<HeaderRecord>().ExecuteAsync();

            var filtered = headers.Where(h => string.Equals(h.Name, headerName, StringComparison.OrdinalIgnoreCase));

            var groups = filtered
                .GroupBy(h => h.Value)
                .Select(g => new HeaderValueCount { Value = g.Key, Count = g.LongCount() })
                .OrderByDescending(x => x.Count)
                .Take(topN)
                .ToArray();

            return new ServerResponse<HeaderValueStatsResponse>
            {
                Success = true,
                Data = new HeaderValueStatsResponse
                {
                    TopValues = groups
                }
            };
        }
    }
}

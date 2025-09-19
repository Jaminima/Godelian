using Godelian.Networking.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Godelian.Server.Endpoints.Client.Connection;
using Godelian.Server.Endpoints.Client.HostRecords;
using Godelian.Server.Endpoints.Client.IPAddressing;
using Godelian.Server.Endpoints.Web.Search;
using Godelian.Server.Endpoints.Web.Statistics;
using Godelian.Server.Endpoints.Client.HostRecords.DTOs;
using Godelian.Server.Endpoints.Web.Search.DTOs;
using Godelian.Server.Endpoints.Web.Statistics.DTOs;

namespace Godelian.Server.Endpoints
{
    internal static class EndpointRouter
    {
        // Route based on request type and convert payloads as needed
        public static async Task<ServerResponse> RouteRequest(ClientRequest<object> clientRequest)
        {
            return clientRequest.RequestType switch
            {
                //Client
                ClientRequestType.Connect => await ConnectionEndpoints.ClientConnects(clientRequest),
                ClientRequestType.NewIpRange => await IPAddresingEndpoints.GetNewIPRange(clientRequest),
                ClientRequestType.SubmitIpRange => await HostRecordEndpoints.SubmitHostRecords(ConvertPayload<SubmitHostRecordsRequest>(clientRequest)),

                //Web
                ClientRequestType.ProgressStats => await StatisticsEndpoints.ProgressStatistics(clientRequest),
                ClientRequestType.SearchRecords => await SearchEndpoints.SearchRecords(ConvertPayload<SearchQuery>(clientRequest)),
                ClientRequestType.RecentlyActiveClients => await StatisticsEndpoints.GetRecentlyActiveClients(clientRequest),
                ClientRequestType.IPDistributionStats => await StatisticsEndpoints.GetIPDistributionStats(ConvertPayload<IPDistributionStats>(clientRequest)),
                ClientRequestType.GetRandomRecord => await SearchEndpoints.GetRandomRecord(clientRequest),

                _ => new ServerResponse { Success = false, Message = "Unknown request type." }
            };
        }

        private static ClientRequest<T> ConvertPayload<T>(ClientRequest<object> request) where T : class
        {
            T? data = null;

            if (request.Data is JsonElement json)
            {
                try
                {
                    data = JsonSerializer.Deserialize<T>(json.GetRawText());
                }
                catch
                {
                    // fallthrough to other strategies
                }
            }
            else if (request.Data is T typed)
            {
                data = typed;
            }
            else if (request.Data is not null)
            {
                try
                {
                    string raw = JsonSerializer.Serialize(request.Data);
                    data = JsonSerializer.Deserialize<T>(raw);
                }
                catch
                {
                    // ignored - will stay null
                }
            }

            return new ClientRequest<T>
            {
                RequestType = request.RequestType,
                ClientId = request.ClientId,
                ClientNickname = request.ClientNickname,
                Data = data
            };
        }
    }
}

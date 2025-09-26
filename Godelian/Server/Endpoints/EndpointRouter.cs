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
using Godelian.Server.Endpoints.Client.FeatureRange;
using Godelian.Server.Endpoints.Client.FeatureRange.DTOs;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Godelian.Server.Endpoints
{
    internal static class EndpointRouter
    {
        // Per-endpoint cache lifetime configuration
        // Specify which ClientRequestTypes are cacheable and how long they live for
        private static readonly IReadOnlyDictionary<ClientRequestType, TimeSpan> CachePolicies = new Dictionary<ClientRequestType, TimeSpan>
        {
            // Web/statistics endpoints
            { ClientRequestType.ProgressStats, TimeSpan.FromMinutes(1) },
            { ClientRequestType.RecentlyActiveClients, TimeSpan.FromMinutes(5) },
            { ClientRequestType.IPDistributionStats, TimeSpan.FromMinutes(10) },
            { ClientRequestType.HeaderNamesStats, TimeSpan.FromMinutes(10) },
            { ClientRequestType.HeaderValuesStats, TimeSpan.FromMinutes(10) }
        };

        private sealed class CacheEntry
        {
            public required ServerResponse Response { get; init; }
            public required DateTimeOffset ExpiresAt { get; init; }
        }

        private static readonly ConcurrentDictionary<string, CacheEntry> ResponseCache = new();

        // Route based on request type and convert payloads as needed
        public static async Task<ServerResponse> RouteRequest(ClientRequest<object> clientRequest)
        {
            bool cacheable = CachePolicies.TryGetValue(clientRequest.RequestType, out TimeSpan ttl);
            string? cacheKey = null;

            if (cacheable)
            {
                cacheKey = BuildCacheKey(clientRequest);
                if (TryGetCached(cacheKey, out ServerResponse? cached))
                {
                    return cached;
                }
            }

            ServerResponse response = clientRequest.RequestType switch
            {
                //Client
                ClientRequestType.Connect => await ConnectionEndpoints.ClientConnects(clientRequest),
                ClientRequestType.NewIpRange => await IPAddresingEndpoints.GetNewIPRange(clientRequest),
                ClientRequestType.SubmitIpRange => await HostRecordEndpoints.SubmitHostRecords(ConvertPayload<SubmitHostRecordsRequest>(clientRequest)),
                ClientRequestType.NewFeatureRange => await FeatureRangeEndpoints.GetFeatureRange(clientRequest),
                ClientRequestType.SubmitFeatureRange => await FeatureRangeEndpoints.SubmitFeatureRanges(ConvertPayload<FeatureRangeCollection>(clientRequest)),

                //Web
                ClientRequestType.ProgressStats => await StatisticsEndpoints.ProgressStatistics(clientRequest),
                //ClientRequestType.SearchRecords => await SearchEndpoints.SearchRecords(ConvertPayload<SearchQuery>(clientRequest)),
                ClientRequestType.RecentlyActiveClients => await StatisticsEndpoints.GetRecentlyActiveClients(clientRequest),
                ClientRequestType.IPDistributionStats => await StatisticsEndpoints.GetIPDistributionStats(ConvertPayload<IPDistributionStats>(clientRequest)),
                ClientRequestType.GetRandomRecord => await RandomHostRecordEndpoint.GetRandomRecord(clientRequest),
                ClientRequestType.GetRandomImage => await RandomImageFeatureEndpoint.GetRandomImageFeature(clientRequest),
                ClientRequestType.HeaderNamesStats => await HeaderStatisticsEndpoints.GetTopHeaderNames(ConvertPayload<HeaderStatsRequest>(clientRequest)),
                ClientRequestType.HeaderValuesStats => await HeaderStatisticsEndpoints.GetTopHeaderValues(ConvertPayload<HeaderValueStatsRequest>(clientRequest)),

                _ => new ServerResponse { Success = false, Message = "Unknown request type." }
            };

            if (cacheable && cacheKey is not null && response.Success)
            {
                SetCached(cacheKey, response, ttl);
            }

            return response;
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

        private static bool TryGetCached(string key, out ServerResponse? response)
        {
            response = null;
            if (!ResponseCache.TryGetValue(key, out CacheEntry? entry))
                return false;

            if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                // remove expired entry
                ResponseCache.TryRemove(key, out _);
                return false;
            }

            response = entry.Response;
            return true;
        }

        private static void SetCached(string key, ServerResponse response, TimeSpan ttl)
        {
            ResponseCache[key] = new CacheEntry
            {
                Response = response,
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttl)
            };
        }

        private static string BuildCacheKey(ClientRequest<object> request)
        {
            // Build a key from RequestType + serialized Data JSON
            string requestTypePart = request.RequestType.ToString();
            string dataJson = SerializeDataForKey(request.Data);
            string composite = requestTypePart + "|" + dataJson;

            // Hash to keep key size bounded
            byte[] bytes = Encoding.UTF8.GetBytes(composite);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static string SerializeDataForKey(object? data)
        {
            if (data is null)
                return "null";

            try
            {
                // If it is already a JsonElement, keep its raw JSON text
                if (data is JsonElement el)
                {
                    return el.GetRawText();
                }

                // Otherwise just serialize the object
                return JsonSerializer.Serialize(data);
            }
            catch
            {
                // Fallback to ToString if serialization fails
                return data.ToString() ?? "null";
            }
        }
    }
}

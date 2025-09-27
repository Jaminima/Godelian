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
using System.Threading;

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
            { ClientRequestType.RecentlyActiveClients, TimeSpan.FromMinutes(1) },
            { ClientRequestType.IPDistributionStats, TimeSpan.FromMinutes(10) },
            { ClientRequestType.HeaderNamesStats, TimeSpan.FromMinutes(10) },
            { ClientRequestType.HeaderValuesStats, TimeSpan.FromMinutes(10) }
        };

        // Define a set of client requests to keep a warm copy of.
        private sealed class WarmRequestSpec
        {
            public required ClientRequestType RequestType { get; init; }
            public object? Data { get; init; }
        }

        private static readonly WarmRequestSpec[] WarmRequests = new[]
        {
            // Keep these frequently requested endpoints warm
            new WarmRequestSpec { RequestType = ClientRequestType.ProgressStats, Data = null },
            new WarmRequestSpec { RequestType = ClientRequestType.RecentlyActiveClients, Data = null },

            new WarmRequestSpec { RequestType = ClientRequestType.IPDistributionStats, Data = new IPDistributionStats { NumBuckets = 64 } },
            new WarmRequestSpec { RequestType = ClientRequestType.IPDistributionStats, Data = new IPDistributionStats { NumBuckets = 128 } },
            new WarmRequestSpec { RequestType = ClientRequestType.IPDistributionStats, Data = new IPDistributionStats { NumBuckets = 256 } },
            new WarmRequestSpec { RequestType = ClientRequestType.IPDistributionStats, Data = new IPDistributionStats { NumBuckets = 512 } },
            new WarmRequestSpec { RequestType = ClientRequestType.IPDistributionStats, Data = new IPDistributionStats { NumBuckets = 1024 } },

            new WarmRequestSpec { RequestType = ClientRequestType.HeaderNamesStats, Data = new HeaderStatsRequest { TopN = 100 } },
            new WarmRequestSpec { RequestType = ClientRequestType.HeaderValuesStats, Data = new HeaderValueStatsRequest { HeaderName = "Server", TopN = 100 } },
            // Add more here; if an endpoint requires a payload, set Data accordingly.
            // new WarmRequestSpec { RequestType = ClientRequestType.IPDistributionStats, Data = new IPDistributionStats { ... } },
        };

        private sealed class CacheEntry
        {
            public required ServerResponse Response { get; init; }
            public required DateTimeOffset ExpiresAt { get; init; }
        }

        private static readonly ConcurrentDictionary<string, CacheEntry> ResponseCache = new();

        // Background warming loop
        private static readonly CancellationTokenSource warmCts = new();
        private static readonly Task warmLoopTask = RunWarmLoopAsync(warmCts.Token);

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

            ServerResponse response = await ExecuteEndpointAsync(clientRequest);

            if (cacheable && cacheKey is not null && response.Success)
            {
                SetCached(cacheKey, response, ttl);
            }

            return response;
        }

        // Centralized endpoint execution so the warmer can reuse it without cache recursion
        private static async Task<ServerResponse> ExecuteEndpointAsync(ClientRequest<object> clientRequest)
        {
            switch (clientRequest.RequestType)
            {
                // Client
                case ClientRequestType.Connect:
                    return await ConnectionEndpoints.ClientConnects(clientRequest);
                case ClientRequestType.NewIpRange:
                    return await IPAddresingEndpoints.GetNewIPRange(clientRequest);
                case ClientRequestType.SubmitIpRange:
                    return await HostRecordEndpoints.SubmitHostRecords(ConvertPayload<SubmitHostRecordsRequest>(clientRequest));
                case ClientRequestType.NewFeatureRange:
                    return await FeatureRangeEndpoints.GetFeatureRange(clientRequest);
                case ClientRequestType.SubmitFeatureRange:
                    return await FeatureRangeEndpoints.SubmitFeatureRanges(ConvertPayload<FeatureRangeCollection>(clientRequest));

                // Web
                case ClientRequestType.ProgressStats:
                    return await StatisticsEndpoints.ProgressStatistics(clientRequest);
                //case ClientRequestType.SearchRecords:
                //    return await SearchEndpoints.SearchRecords(ConvertPayload<SearchQuery>(clientRequest));
                case ClientRequestType.RecentlyActiveClients:
                    return await StatisticsEndpoints.GetRecentlyActiveClients(clientRequest);
                case ClientRequestType.IPDistributionStats:
                    return await StatisticsEndpoints.GetIPDistributionStats(ConvertPayload<IPDistributionStats>(clientRequest));
                case ClientRequestType.GetRandomRecord:
                    return await RandomHostRecordEndpoint.GetRandomRecord(clientRequest);
                case ClientRequestType.GetRandomImage:
                    return await RandomImageFeatureEndpoint.GetRandomImageFeature(clientRequest);
                case ClientRequestType.HeaderNamesStats:
                    return await HeaderStatisticsEndpoints.GetTopHeaderNames(ConvertPayload<HeaderStatsRequest>(clientRequest));
                case ClientRequestType.HeaderValuesStats:
                    return await HeaderStatisticsEndpoints.GetTopHeaderValues(ConvertPayload<HeaderValueStatsRequest>(clientRequest));
                default:
                    return new ServerResponse { Success = false, Message = "Unknown request type." };
            }
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

        private static bool TryGetCacheEntry(string key, out CacheEntry? entry)
        {
            if (ResponseCache.TryGetValue(key, out entry))
            {
                if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    ResponseCache.TryRemove(key, out _);
                    entry = null;
                    return false;
                }
                return true;
            }
            entry = null;
            return false;
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

        private static async Task RunWarmLoopAsync(CancellationToken token)
        {
            // Small tick to check if any warm entries need refreshing
            TimeSpan checkInterval = TimeSpan.FromSeconds(5);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    foreach (var spec in WarmRequests)
                    {
                        if (!CachePolicies.TryGetValue(spec.RequestType, out TimeSpan ttl))
                            continue; // only warm cacheable endpoints

                        var req = new ClientRequest<object>
                        {
                            RequestType = spec.RequestType,
                            ClientId = null,
                            ClientNickname = null,
                            TaskId = null,
                            Data = spec.Data
                        };

                        string key = BuildCacheKey(req);

                        bool needsRefresh = true;
                        if (TryGetCacheEntry(key, out CacheEntry? entry))
                        {
                            // Refresh a bit before expiry to keep it warm
                            var remaining = entry!.ExpiresAt - DateTimeOffset.UtcNow;
                            needsRefresh = remaining <= TimeSpan.FromSeconds(Math.Max(5, ttl.TotalSeconds * 0.1));
                        }

                        if (needsRefresh)
                        {
                            ServerResponse resp = await ExecuteEndpointAsync(req);
                            if (resp.Success)
                            {
                                SetCached(key, resp, ttl);
                            }
                        }
                    }
                }
                catch
                {
                    // swallow warming errors; they'll be retried on next tick
                }

                try
                {
                    await Task.Delay(checkInterval, token);
                }
                catch (TaskCanceledException)
                {
                    // exiting
                }
            }
        }
    }
}

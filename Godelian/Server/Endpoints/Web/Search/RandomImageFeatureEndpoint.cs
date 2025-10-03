using Godelian.Models;
using Godelian.Networking.DTOs;
using System.Collections.Concurrent;

namespace Godelian.Server.Endpoints.Web.Search
{
    internal static class RandomImageFeatureEndpoint
    {
        private static readonly ConcurrentQueue<FeatureDTO> recentImageFeatures = new();
        private const int RecentQueueCapacity = 100;

        internal static void EnqueueRecentImage(FeatureDTO feature)
        {
            if (feature is null) return;
            if (string.IsNullOrEmpty(feature.Base64Content)) return; 

            recentImageFeatures.Enqueue(feature);

            while (recentImageFeatures.Count > RecentQueueCapacity && recentImageFeatures.TryDequeue(out _)) { }
        }

        public static async Task<ServerResponse<FeatureDTO>> GetRandomImageFeature(ClientRequest<object> clientRequest)
        {
            if (recentImageFeatures.TryDequeue(out FeatureDTO? recent))
            {
                return new ServerResponse<FeatureDTO>
                {
                    Success = true,
                    Data = recent
                };
            }

            return new ServerResponse<FeatureDTO>
            {
                Success = false,
                Message = "No recent images available"
            };
        }
    }
}

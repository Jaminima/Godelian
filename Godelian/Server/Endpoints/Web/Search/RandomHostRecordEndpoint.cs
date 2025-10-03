using Godelian.Models;
using Godelian.Networking.DTOs;
using System.Collections.Concurrent;

namespace Godelian.Server.Endpoints.Web.Search
{
    internal static class RandomHostRecordEndpoint
    {
        private static readonly ConcurrentQueue<HostRecordModelDTO> recentHostRecords = new();
        private const int RecentQueueCapacity = 100;

        internal static void EnqueueRecentHostRecord(HostRecordModelDTO record)
        {
            if (record is null) return;
            recentHostRecords.Enqueue(record);

            while (recentHostRecords.Count > RecentQueueCapacity && recentHostRecords.TryDequeue(out _)) { }
        }

        public static async Task<ServerResponse<HostRecordModelDTO>> GetRandomRecord(ClientRequest<object> clientRequest)
        {
            if (recentHostRecords.TryDequeue(out var recent))
            {
                return new ServerResponse<HostRecordModelDTO>
                {
                    Success = true,
                    Data = recent
                };
            }

            return new ServerResponse<HostRecordModelDTO>
            {
                Success = false,
                Message = "No recent host records available"
            };
        }
    }
}

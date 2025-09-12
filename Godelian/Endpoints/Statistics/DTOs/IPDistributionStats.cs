namespace Godelian.Endpoints.Statistics.DTOs
{
    internal class IPDistributionStats
    {
        public required int NumBuckets { get; set; }
    }

    internal class IPDistributionStatsResponse
    {
        public required int[] NumIPs { get; set; }
    }
}

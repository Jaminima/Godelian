namespace Godelian.Server.Endpoints.Web.Statistics.DTOs
{
    internal class IPDistributionStats
    {
        public required int NumBuckets { get; set; }
    }

    internal class IPDistributionBucket
    {
        public required string StartIP { get; set; }
        public required string EndIP { get; set; }
        public required int NumIPs { get; set; }
    }

    internal class IPDistributionStatsResponse
    {
        public required IPDistributionBucket[] Buckets { get; set; }
    }
}

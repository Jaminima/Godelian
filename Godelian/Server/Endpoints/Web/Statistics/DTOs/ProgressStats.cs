namespace Godelian.Server.Endpoints.Web.Statistics.DTOs
{
    internal class ProgressStats
    {         
        public required ulong CurrentIPIndex { get; set; }
        public required string CurrentIPAddress { get; set; }
        public required double PercentageComplete { get; set; }
        public required string EstimatedTimeRemaining { get; set; }
        public required ulong FoundHosts { get; set; }
    }
}

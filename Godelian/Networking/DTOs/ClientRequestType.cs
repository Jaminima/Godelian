namespace Godelian.Networking.DTOs
{
    internal enum ClientRequestType
    {
        Connect,
        Disconnect,
        NewIpRange,
        SubmitIpRange,
        ProgressStats,
        SearchRecords,
        RecentlyActiveClients,
        IPDistributionStats
    }
}

namespace Godelian.Networking.DTOs
{
    internal enum ClientRequestType
    {
        // Client
        Connect,
        Disconnect,
        NewIpRange,
        SubmitIpRange,

        // Web
        ProgressStats,
        SearchRecords,
        RecentlyActiveClients,
        IPDistributionStats,
        GetRandomRecord
    }
}

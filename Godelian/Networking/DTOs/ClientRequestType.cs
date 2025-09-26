namespace Godelian.Networking.DTOs
{
    internal enum ClientRequestType
    {
        // Client
        Connect = 100,
        Disconnect = 101,
        NewIpRange = 102,
        SubmitIpRange = 103,
        NewFeatureRange = 104,
        SubmitFeatureRange = 105,

        // Web
        ProgressStats =200,
        SearchRecords=201,
        RecentlyActiveClients=202,
        IPDistributionStats=203,
        GetRandomRecord=204,
        GetRandomImage=205,
        HeaderNamesStats=206,
        HeaderValuesStats=207
    }
}

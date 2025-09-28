export type ClientRequestDto<T> = {
    RequestType: ClientRequestType;
    Data?: T;
}

export enum ClientRequestType {
        // Client
        Connect = 100,
        Disconnect = 101,
        NewIpRange = 102,
        SubmitIpRange = 103,

        // Web
        ProgressStats=200,
        SearchFeatures=201,
        RecentlyActiveClients=202,
        IPDistributionStats=203,
        GetRandomRecord=204,
        GetRandomImage=205,
        HeaderNamesStats=206,
        HeaderValuesStats=207
}
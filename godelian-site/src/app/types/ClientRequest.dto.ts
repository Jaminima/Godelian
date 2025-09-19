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
        SearchRecords=201,
        RecentlyActiveClients=202,
        IPDistributionStats=203,
        GetRandomRecord=204
}
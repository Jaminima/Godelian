export type ClientRequestDto<T> = {
    RequestType: ClientRequestType;
    Data?: T;
}

export enum ClientRequestType {
        Connect=0,
        Disconnect=1,
        NewIpRange=2,
        SubmitIpRange=3,
        ProgressStats=4,
        GetAllIpIndexes=5,
        RecentlyActiveClients=6,
        IPDistributionStats=7
}
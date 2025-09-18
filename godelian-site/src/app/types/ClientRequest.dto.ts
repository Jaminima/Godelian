export type ClientRequestDto<T> = {
    RequestType: ClientRequestType;
    Data?: T;
}

export enum ClientRequestType {
        //Client -- can ignore
        Connect=0,
        Disconnect=1,
        NewIpRange=2,
        SubmitIpRange=3,

        //WebAPI -- used by Angular app
        ProgressStats=4,
        SearchRecords=5,
        RecentlyActiveClients=6,
        IPDistributionStats=7,
        GetRandomRecord=8,
}
export type IPDistributionStatsRequestDto = {
    NumBuckets: number;
};

export type IPDistributionStatsDto = {
    Buckets: IPDistributionBucketDto[];
};

export type IPDistributionBucketDto = {
    StartIP: string;
    EndIP: string;
    NumIPs: number;
}
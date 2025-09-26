export type HeaderStatsRequestDto = {
    TopN: number;
}

export type HeaderValueStatsRequestDto = {
    HeaderName: string;
    TopN: number;
}

export type HeaderNameCount = {
    Name: string;
    Count: number;
}

export type HeaderValueCount = {
    Value: string;
    Count: number;
}

export type HeaderStatsResponseDto = {
    TopHeaderNames: HeaderNameCount[];
}

export type HeaderValueStatsResponseDto = {
    TopValues: HeaderValueCount[];
}

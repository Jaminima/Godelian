export interface HostRecord {
    IPIndex: number;
    Iteration: number;
    IPAddress: string;
    Hostname: string;
    FoundByClientId: string;
    FoundAt: Date;
    Features: Feature[];
}

export interface Feature {
    Content: string;
    Type: FeatureType;
}

export enum FeatureType {
    Title=0,
    Heading=1,
    Text=2,
    Script=3,
    Image=4,
    Link=5
}

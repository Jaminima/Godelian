export interface HostRecord {
    IPIndex: number;
    Iteration: number;
    IPAddress: string;
    Hostname: string;
    FoundByClientId: string;
    FoundAt: Date;
    Features?: Feature[];
    HeaderRecords?: HeaderRecord[];
    FeaturesElaorated: boolean;
    HostRequestMethod: HostRequestMethod;
}

export interface HeaderRecord{
    HostRecordId?: string;
    Name: string;
    Value: string;

}

export interface Feature {
    Content?: string;
    Base64Content?: string; // For images
    Type: FeatureType;
    HostRecord?: HostRecord;
}

export enum HostRequestMethod{      
    HTTP=0,
    HTTPS=1,
}

export enum FeatureType {
    Title=0,
    Heading=1,
    Text=2,
    Script=3,
    Image=4,
    Link=5,
    Base64=6
}

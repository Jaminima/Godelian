import { Feature } from "./HostRecord.dto";

export type SearchQuery = {
    Query: string;
}

export type SearchResponse = {
    Features: Feature[];
}
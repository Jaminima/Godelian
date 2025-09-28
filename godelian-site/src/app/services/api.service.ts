import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ClientRequestDto, ClientRequestType } from '../types/ClientRequest.dto';
import { HeaderStatsRequestDto, HeaderStatsResponseDto, HeaderValueStatsRequestDto, HeaderValueStatsResponseDto } from '../types/HeaderStats.dto';
import { Feature, HostRecord } from '../types/HostRecord.dto';
import { IPDistributionStatsDto } from '../types/IPDistributionStats.dto';
import { ProgressStatsDto } from '../types/ProgressStats.dto';
import { RecentClientsDto } from '../types/RecentClients.dto';
import { SearchResponse } from '../types/SearchFeatures.dto';
import { ServerResponseDto } from '../types/ServerResponse.dto';

export interface SearchResult {
  // Based on the C# backend structure
  [key: string]: any;
}

export interface LiveServerStats {
  activeClients: number;
  completedBatches: number;
  totalBatches: number;
  percent: number;
  elapsed: string;
  eta: string;
}

export interface SearchStatsResponse {
  totalStoredResults: number;
  lastCompletedBatch: number;
  lastUpdated?: string;
  liveStats?: LiveServerStats;
}

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly baseUrl: string;

  constructor(private readonly http: HttpClient) {
    // Use the same host as the current page, but with port 8080
    // This works for both localhost development and deployed environments
    const protocol = window.location.protocol;
    const hostname = window.location.hostname;

    if (protocol === 'https:') {
      this.baseUrl = `https://${hostname}/api/`;
    }else{
      this.baseUrl = `${protocol}//${hostname}:9000`;
    }
  }

  sendRequest<TI, TO>(req: ClientRequestDto<TI>): Observable<ServerResponseDto<TO>> {
    return this.http.post<ServerResponseDto<TO>>(`${this.baseUrl}`, req);
  }

  getStats(): Observable<ServerResponseDto<ProgressStatsDto>> {
    return this.sendRequest<null, ProgressStatsDto>({ RequestType: ClientRequestType.ProgressStats });
  }

  getRecentClients(): Observable<ServerResponseDto<RecentClientsDto>> {
    return this.sendRequest<null, RecentClientsDto>({ RequestType: ClientRequestType.RecentlyActiveClients });
  }

  getIPDistributionStats(numBuckets: number): Observable<ServerResponseDto<IPDistributionStatsDto>> {
    return this.sendRequest<{ NumBuckets: number }, IPDistributionStatsDto>({
      RequestType: ClientRequestType.IPDistributionStats,
      Data: { NumBuckets: numBuckets }
    });
  }


  getRandomRecord(): Observable<ServerResponseDto<HostRecord>> {
    return this.sendRequest<null, HostRecord>({ RequestType: ClientRequestType.GetRandomRecord });
  }

  getRandomImage(): Observable<ServerResponseDto<Feature>> {
    return this.sendRequest<null, Feature>({ RequestType: ClientRequestType.GetRandomImage });
  }

  getHeaderNamesStats(topN: number): Observable<ServerResponseDto<HeaderStatsResponseDto>> {
    return this.sendRequest<HeaderStatsRequestDto, HeaderStatsResponseDto>({
      RequestType: ClientRequestType.HeaderNamesStats,
      Data: { TopN: topN }
    });
  }

  getHeaderValuesStats(headerName: string, topN: number): Observable<ServerResponseDto<HeaderValueStatsResponseDto>> {
    return this.sendRequest<HeaderValueStatsRequestDto, HeaderValueStatsResponseDto>({
      RequestType: ClientRequestType.HeaderValuesStats,
      Data: { HeaderName: headerName, TopN: topN }
    });
  }

  searchFeatures(query: string): Observable<ServerResponseDto<SearchResponse>> {
    return this.sendRequest<{ Query: string }, SearchResponse>({
      RequestType: ClientRequestType.SearchFeatures,
      Data: { Query: query }
    });
  }
}

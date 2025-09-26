import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { ApiService } from '../services/api.service';
import { HeaderNameCount, HeaderValueCount } from '../types/HeaderStats.dto';

@Component({
  selector: 'app-header-stats',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './header-stats.component.html',
  styleUrls: ['./header-stats.component.scss']
})
export class HeaderStatsComponent implements OnInit {
  private readonly api = inject(ApiService);

  headerNames: HeaderNameCount[] = [];
  headerValues: HeaderValueCount[] = [];
  selectedHeader: string | null = null;
  loading = false;
  errorMsg = '';

  ngOnInit(): void {
    this.loadHeaderNames();
  }

  loadHeaderNames() {
    this.loading = true;
    this.errorMsg = '';
    this.api.getHeaderNamesStats(100).subscribe({
      next: (res) => {
        this.headerNames = res.Data?.TopHeaderNames ?? [];
        this.headerValues = [];
        this.selectedHeader = null;
        this.loading = false;
      },
      error: (err) => {
        this.errorMsg = err?.message ?? 'Failed to load header names';
        this.loading = false;
      }
    });
  }

  loadHeaderValues(headerName: string) {
    this.loading = true;
    this.errorMsg = '';
    this.api.getHeaderValuesStats(headerName, 100).subscribe({
      next: (res) => {
        this.headerValues = res.Data?.TopValues ?? [];
        this.selectedHeader = headerName;
        this.loading = false;
      },
      error: (err) => {
        this.errorMsg = err?.message ?? 'Failed to load header values';
        this.loading = false;
      }
    });
  }

  backToNames() {
    this.selectedHeader = null;
    this.headerValues = [];
  }
}
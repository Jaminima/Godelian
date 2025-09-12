import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, inject, OnDestroy, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { interval, Subscription } from 'rxjs';
import { ApiService } from '../services/api.service';
import { ProgressStatsDto } from '../types/ProgressStats.dto';
import { ServerResponseDto } from '../types/ServerResponse.dto';

@Component({
  selector: 'app-stats-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, BaseChartDirective],
  templateUrl: './stats-dashboard.component.html',
  styleUrls: ['./stats-dashboard.component.scss']
})
export class StatsDashboardComponent implements OnInit, OnDestroy, AfterViewInit {
  private readonly apiService = inject(ApiService);
  private refreshSubscription?: Subscription;

  stats = signal<ServerResponseDto<ProgressStatsDto> | null>(null);
  errorMessage = signal('');

  // IP distribution & chart state
  readonly bucketOptions = [64, 256, 512, 1024];
  selectedBuckets = signal<number>(64);
  ipDistribution = signal<number[] | null>(null);

  @ViewChild(BaseChartDirective) chart?: BaseChartDirective;

  // ng2-charts reactive bindings
  public barChartData: ChartData<'bar'> = { labels: [], datasets: [{ data: [], label: 'IPs per bucket' }] };
  public barChartOptions: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: { title: { display: true, text: 'Bucket' } },
      y: { title: { display: true, text: 'Number of IPs' }, beginAtZero: true }
    }
  };

  ngOnInit() {
    this.loadStats();
    // Auto-refresh every 60 seconds
    this.refreshSubscription = interval(60000).subscribe(() => {
      this.loadStats();
    });
  }

  ngAfterViewInit() {
    // Trigger initial load for IP distribution. The baseChart directive will render when data is set.
    this.loadIPDistribution(this.selectedBuckets());
  }

  ngOnDestroy() {
    this.refreshSubscription?.unsubscribe();
  }

  loadStats() {
    this.apiService.getStats().subscribe({
      next: (stats) => {
        this.stats.set(stats);
        this.errorMessage.set('');
      },
      error: (error) => {
        this.errorMessage.set(`Failed to load stats: ${error.message || 'Unknown error'}`);
      }
    });
  }

  // Fetch IP distribution data from the API and update the chart
  loadIPDistribution(numBuckets: number) {
    this.apiService.getIPDistributionStats(numBuckets).subscribe({
      next: (res) => {
        if (res?.Data && Array.isArray(res.Data.NumIPs)) {
          this.ipDistribution.set(res.Data.NumIPs);
          this.updateChartData(res.Data.NumIPs);
        } else {
          this.ipDistribution.set(null);
        }
      },
      error: (err) => {
        console.error('Failed to load IP distribution', err);
        this.ipDistribution.set(null);
      }
    });
  }

  onBucketsChange(eventOrValue: Event | string | number) {
    let value: string | number | undefined;
    if (typeof eventOrValue === 'string' || typeof eventOrValue === 'number') {
      value = eventOrValue;
    } else if (eventOrValue && typeof eventOrValue === 'object' && 'target' in eventOrValue) {
      const target = (eventOrValue as Event & { target?: HTMLSelectElement }).target;
      value = target ? target.value : undefined;
    }

    const n = typeof value === 'string' ? parseInt(value, 10) : Number(value);
    if (!isFinite(n) || n <= 0) return;
    this.selectedBuckets.set(n);
    this.loadIPDistribution(n);
  }

  private updateChartData(data: number[]) {
    const labels = data.map((_, i) => `B${i + 1}`);
    this.barChartData = { labels, datasets: [{ data: data as number[], label: 'IPs per bucket', backgroundColor: 'rgba(14,165,233,0.8)' }] };
    // Ensure chart updates if the directive instance is present
    setTimeout(() => this.chart?.update(), 0);
  }
}

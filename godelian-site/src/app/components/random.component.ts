import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ApiService } from '../services/api.service';
import { HostRecord } from '../types/HostRecord.dto';

@Component({
  selector: 'app-random',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './random.component.html',
  styleUrls: ['./random.component.scss']
})
export class RandomComponent {
  private readonly api = inject(ApiService);
  record: HostRecord | null = null;
  loading = false;
  errorMsg = '';

  fetchRandom() {
    this.loading = true;
    this.errorMsg = '';
    this.api.getRandomRecord().subscribe({
      next: (res) => {
        this.record = res.Data ?? null;
        this.loading = false;
      },
      error: (err) => {
        this.errorMsg = err?.message ?? 'Failed to fetch random record';
        this.loading = false;
      }
    });
  }

  openLucky() {
    if (this.record) {
      const host = this.record.Hostname || this.record.IPAddress;
      const url = `http://${host}`;
      window.open(url, '_blank');
    }
  }
}

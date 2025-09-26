import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ApiService } from '../services/api.service';
import { HostRecord } from '../types/HostRecord.dto';

@Component({
  selector: 'app-random-image',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './random-image.component.html',
  styleUrls: ['./random-image.component.scss']
})
export class RandomImageComponent {
  private readonly api = inject(ApiService);
  record: HostRecord | null = null;
  imageData: string | null = null;
  loading = false;
  errorMsg = '';

  fetchRandomImage() {
    this.loading = true;
    this.errorMsg = '';
    this.record = null;
    this.imageData = null;
    this.api.getRandomImage().subscribe({
      next: (imgRes) => {
        this.record = imgRes.Data?.HostRecord ?? null;
        this.imageData = imgRes.Data?.Base64Content ?? null;
        this.loading = false;
      },
      error: (err) => {
        this.errorMsg = err?.message ?? 'Failed to fetch random image';
        this.loading = false;
      }
    });
  }
}
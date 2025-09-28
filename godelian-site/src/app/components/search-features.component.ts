import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../services/api.service';
import { Feature } from '../types/HostRecord.dto';

@Component({
  selector: 'app-search-features',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './search-features.component.html',
  styleUrls: ['./search-features.component.scss']
})
export class SearchFeaturesComponent {
  private readonly api = inject(ApiService);
  query = '';
  features: Feature[] = [];
  loading = false;
  errorMsg = '';

  search() {
    if (!this.query.trim()) {
      this.errorMsg = 'Please enter a search query';
      return;
    }

    this.loading = true;
    this.errorMsg = '';
    this.features = [];

    this.api.searchFeatures(this.query).subscribe({
      next: (res) => {
        this.features = res.Data?.Features ?? [];
        this.loading = false;
      },
      error: (err) => {
        this.errorMsg = err?.message ?? 'Failed to search features';
        this.loading = false;
      }
    });
  }

  getFeatureTypeName(type: number): string {
    const types = ['Title', 'Heading', 'Text', 'Script', 'Image', 'Link', 'Base64'];
    return types[type] || 'Unknown';
  }

  isImageFeature(feature: Feature): boolean {
    return feature.Type === 4 || feature.Type === 6; // Image or Base64
  }

  getImageSrc(feature: Feature): string {
    if (feature.Base64Content) {
      return `data:image/png;base64,${feature.Base64Content}`;
    }
    return feature.Content || '';
  }
}
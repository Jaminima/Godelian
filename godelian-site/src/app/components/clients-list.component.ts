import { CommonModule } from '@angular/common';
import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { ApiService } from '../services/api.service';
import { ClientDto, RecentClientsDto } from '../types/RecentClients.dto';

type GroupedClients = {
  key: string; // nickname or short id
  nickname?: string;
  clients: ClientDto[];
};

@Component({
  selector: 'app-clients-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './clients-list.component.html',
  styleUrls: ['./clients-list.component.scss']
})
export class ClientsListComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private refreshSub?: Subscription;

  recent = signal<RecentClientsDto | null>(null);
  grouped = signal<GroupedClients[]>([]);
  errorMessage = signal('');

  ngOnInit(): void {
    this.loadClients();
    // refresh every 60s
    this.refreshSub = interval(60000).subscribe(() => this.loadClients());
  }

  ngOnDestroy(): void {
    this.refreshSub?.unsubscribe();
  }

  loadClients() {
    this.api.getRecentClients().subscribe({
      next: (res) => {
        this.recent.set(res.Data ?? null);
        this.errorMessage.set('');
        this.groupByNickname(res.Data?.Clients ?? []);
      },
      error: (err) => {
        this.errorMessage.set(err?.message ?? 'Failed to load recent clients');
      }
    });
  }

  private groupByNickname(clients: ClientDto[]) {
    const map = new Map<string, ClientDto[]>();

    for (const c of clients) {
      const nick = (c.Nickname && c.Nickname.trim().length > 0) ? c.Nickname.trim() : undefined;
      const key = nick ?? c.ClientId.substring(0, 8);

      const arr = map.get(key) ?? [];
      arr.push(c);
      map.set(key, arr);
    }

    const grouped: GroupedClients[] = [];
    for (const [key, arr] of map.entries()) {
      const clientsSorted = arr.slice().sort((a, b) => new Date(b.LastActiveAt).getTime() - new Date(a.LastActiveAt).getTime());
      grouped.push({ key, nickname: arr[0].Nickname, clients: clientsSorted });
    }

    // sort groups by most recently active client
    grouped.sort((a,b)=> a.key < b.key ? -1 : (a.key > b.key ? 1 : 0));
    this.grouped.set(grouped);
  }
}

import { Routes } from '@angular/router';
import { ClientsListComponent } from './components/clients-list.component';
import { StatsDashboardComponent } from './components/stats-dashboard.component';

export const routes: Routes = [
	{ path: '', redirectTo: 'stats', pathMatch: 'full' },
	{ path: 'stats', component: StatsDashboardComponent },
	{ path: 'clients', component: ClientsListComponent },
	{ path: '**', redirectTo: 'stats' }
];

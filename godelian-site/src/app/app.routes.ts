import { Routes } from '@angular/router';
import { ClientsListComponent } from './components/clients-list.component';
import { HeaderStatsComponent } from './components/header-stats.component';
import { RandomImageComponent } from './components/random-image.component';
import { RandomComponent } from './components/random.component';
import { SearchFeaturesComponent } from './components/search-features.component';
import { StatsDashboardComponent } from './components/stats-dashboard.component';

export const routes: Routes = [
	{ path: '', redirectTo: 'stats', pathMatch: 'full' },
	{ path: 'stats', component: StatsDashboardComponent },
	{ path: 'clients', component: ClientsListComponent },
	{ path: 'random', component: RandomComponent },
	{ path: 'image', component: RandomImageComponent },
	{ path: 'headers', component: HeaderStatsComponent },
	{ path: 'search', component: SearchFeaturesComponent },
	{ path: '**', redirectTo: 'stats' }
];

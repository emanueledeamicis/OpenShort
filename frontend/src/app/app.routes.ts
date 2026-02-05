import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login/login';
import { MainLayoutComponent } from './core/layout/main-layout/main-layout';
import { DashboardComponent } from './features/dashboard/dashboard/dashboard';
import { LinksListComponent } from './features/links/links-list/links-list';
import { DomainsListComponent } from './features/domains/domains-list/domains-list';
import { SecurityComponent } from './features/security/security.component';
import { authGuard } from './core/guards/auth-guard';

export const routes: Routes = [
    { path: 'login', component: LoginComponent },
    {
        path: '',
        component: MainLayoutComponent,
        canActivate: [authGuard],
        children: [
            { path: 'dashboard', component: DashboardComponent },
            { path: 'links', component: LinksListComponent },
            { path: 'domains', component: DomainsListComponent },
            { path: 'security', component: SecurityComponent },
            { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
        ]
    },
    { path: '**', redirectTo: 'dashboard' }
];

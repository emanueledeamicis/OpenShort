import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-main-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule],
  template: `
    <div class="flex min-h-screen bg-surface-50 dark:bg-surface-950">
      <!-- Sidebar -->
      <aside class="w-64 bg-surface-0 dark:bg-surface-900 border-r border-surface-200 dark:border-surface-700 p-4 flex flex-col">
        <div class="flex items-center gap-2 mb-8 px-2">
            <span class="text-xl font-bold text-primary-500">OpenShort</span>
        </div>
        
        <nav class="flex-1 flex flex-col gap-2">
            <a routerLink="/dashboard" routerLinkActive="bg-primary-50 dark:bg-primary-900/20 text-primary-600 dark:text-primary-400" 
               class="p-3 rounded-lg flex items-center gap-3 text-surface-600 dark:text-surface-400 hover:bg-surface-100 dark:hover:bg-surface-800 transition-colors">
                <i class="pi pi-home"></i>
                <span class="font-medium">Dashboard</span>
            </a>
            <a routerLink="/links" routerLinkActive="bg-primary-50 dark:bg-primary-900/20 text-primary-600 dark:text-primary-400" 
               class="p-3 rounded-lg flex items-center gap-3 text-surface-600 dark:text-surface-400 hover:bg-surface-100 dark:hover:bg-surface-800 transition-colors">
                <i class="pi pi-link"></i>
                <span class="font-medium">Links</span>
            </a>
             <a routerLink="/domains" routerLinkActive="bg-primary-50 dark:bg-primary-900/20 text-primary-600 dark:text-primary-400" 
               class="p-3 rounded-lg flex items-center gap-3 text-surface-600 dark:text-surface-400 hover:bg-surface-100 dark:hover:bg-surface-800 transition-colors">
                <i class="pi pi-globe"></i>
                <span class="font-medium">Domains</span>
            </a>
        </nav>

        <div class="mt-auto pt-4 border-t border-surface-200 dark:border-surface-700">
             <button 
                (click)="logout()"
                class="w-full p-3 rounded-lg flex items-center gap-3 text-surface-600 dark:text-surface-400 hover:bg-surface-100 dark:hover:bg-surface-800 transition-colors text-left">
                <i class="pi pi-sign-out"></i>
                <span class="font-medium">Logout</span>
            </button>
        </div>
      </aside>

      <!-- Main Content -->
      <main class="flex-1 p-8">
        <router-outlet></router-outlet>
      </main>
    </div>
  `
})
export class MainLayoutComponent {
  constructor(
    private authService: AuthService,
    private router: Router
  ) { }

  logout() {
    this.authService.logout().subscribe({
      next: () => {
        this.router.navigate(['/login']);
      },
      error: (err) => {
        console.error('Logout error:', err);
        // Force navigation even if logout fails
        this.router.navigate(['/login']);
      }
    });
  }
}

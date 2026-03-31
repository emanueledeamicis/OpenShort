import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardModule } from 'primeng/card';
import { catchError, forkJoin, of } from 'rxjs';
import { LinkService } from '../../../core/services/link.service';
import { DomainService } from '../../../core/services/domain.service';
import { UpdateService } from '../../../core/services/update.service';
import packageJson from '../../../../../package.json';

@Component({
   selector: 'app-dashboard',
   standalone: true,
   imports: [CommonModule, CardModule],
   template: `
    <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
       <!-- Welcome Card -->
       <div class="col-span-full">
         <div class="bg-surface-0 dark:bg-surface-900 p-6 rounded-xl border border-surface-200 dark:border-surface-700 shadow-sm">
            <h1 class="text-2xl font-bold text-surface-900 dark:text-surface-0 mb-2">Welcome Back!</h1>
            <p class="text-surface-600 dark:text-surface-400">Here's what's happening with your short links today.</p>
         </div>
       </div>

       <div class="col-span-full" *ngIf="loadError">
         <div class="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-amber-800">
            {{ loadError }}
         </div>
       </div>

       <div class="col-span-full" *ngIf="showUpdateMessage">
         <div class="flex flex-col gap-3 rounded-xl border border-sky-200 bg-sky-50 px-4 py-4 text-sky-900 md:flex-row md:items-center md:justify-between">
            <div>
               <p class="text-sm font-semibold uppercase tracking-[0.18em] text-sky-700">Update available</p>
               <p class="mt-1 text-sm md:text-base">{{ updateMessage }}</p>
            </div>
            <a
               class="inline-flex items-center justify-center rounded-lg border border-sky-300 bg-white px-4 py-2 text-sm font-semibold text-sky-700 transition hover:border-sky-400 hover:bg-sky-100"
               [href]="releaseUrl"
               target="_blank"
               rel="noopener noreferrer"
            >
               View release
            </a>
         </div>
       </div>

       <!-- Total Links -->
       <div class="bg-surface-0 dark:bg-surface-900 p-6 rounded-xl border border-surface-200 dark:border-surface-700 shadow-sm">
          <div class="flex items-center justify-between mb-4">
             <h3 class="text-lg font-semibold text-surface-700 dark:text-surface-300">Total Links</h3>
             <i class="pi pi-link text-2xl text-primary-500"></i>
          </div>
          <div class="text-4xl font-bold text-primary-500" *ngIf="!loading">{{ totalLinks }}</div>
          <div class="text-4xl font-bold text-surface-400" *ngIf="loading">--</div>
       </div>

       <!-- Active Domains -->
       <div class="bg-surface-0 dark:bg-surface-900 p-6 rounded-xl border border-surface-200 dark:border-surface-700 shadow-sm">
          <div class="flex items-center justify-between mb-4">
             <h3 class="text-lg font-semibold text-surface-700 dark:text-surface-300">Active Domains</h3>
             <i class="pi pi-globe text-2xl text-primary-500"></i>
          </div>
          <div class="text-4xl font-bold text-primary-500" *ngIf="!loading">{{ activeDomains }}</div>
          <div class="text-4xl font-bold text-surface-400" *ngIf="loading">--</div>
       </div>
    </div>
  `
})
export class DashboardComponent implements OnInit {
   readonly releaseUrl = 'https://github.com/emanueledeamicis/OpenShort/releases/latest';
   currentVersion = packageJson.version;
   latestVersion: string | null = null;
   showUpdateMessage = false;
   totalLinks = 0;
   activeDomains = 0;
   loading = true;
   loadError = '';

   get updateMessage(): string {
      if (!this.latestVersion) {
         return '';
      }

      return `A new version of OpenShort is available: v${this.latestVersion}. You are currently running v${this.currentVersion}.`;
   }

   constructor(
      private linkService: LinkService,
      private domainService: DomainService,
      private updateService: UpdateService,
      private cdr: ChangeDetectorRef
   ) { }

   ngOnInit() {
      this.loadStats();
      this.loadUpdateStatus();
   }

   loadStats() {
      this.loading = true;
      this.loadError = '';

      forkJoin({
         links: this.linkService.getAll().pipe(
            catchError(() => {
               this.loadError = 'Some dashboard data could not be loaded.';
               return of([]);
            })
         ),
         domains: this.domainService.getAll().pipe(
            catchError(() => {
               this.loadError = 'Some dashboard data could not be loaded.';
               return of([]);
            })
         )
      }).subscribe({
         next: ({ links, domains }) => {
            this.totalLinks = links.length;
            this.activeDomains = domains.filter(d => d.isActive).length;
            this.loading = false;
            this.cdr.detectChanges();
         }
      });
   }

   loadUpdateStatus() {
      this.updateService.getLatestVersion().pipe(
         catchError(() => of({ latestVersion: null }))
      ).subscribe({
         next: ({ latestVersion }) => {
            this.latestVersion = this.normalizeVersion(latestVersion);
            this.showUpdateMessage = this.hasNewerVersion(this.currentVersion, this.latestVersion);
            this.cdr.detectChanges();
         }
      });
   }

   private hasNewerVersion(currentVersion: string | null, latestVersion: string | null): boolean {
      if (!currentVersion || !latestVersion) {
         return false;
      }

      const currentParts = this.parseVersion(currentVersion);
      const latestParts = this.parseVersion(latestVersion);
      const maxLength = Math.max(currentParts.length, latestParts.length);

      for (let index = 0; index < maxLength; index++) {
         const currentPart = currentParts[index] ?? 0;
         const latestPart = latestParts[index] ?? 0;

         if (latestPart > currentPart) {
            return true;
         }

         if (latestPart < currentPart) {
            return false;
         }
      }

      return false;
   }

   private parseVersion(version: string): number[] {
      const normalizedVersion = this.normalizeVersion(version) ?? '0';

      return normalizedVersion
         .split('.')
         .map(part => Number.parseInt(part, 10))
         .filter(part => Number.isFinite(part));
   }

   private normalizeVersion(version: string | null): string | null {
      return version?.trim().replace(/^v/i, '') || null;
   }
}

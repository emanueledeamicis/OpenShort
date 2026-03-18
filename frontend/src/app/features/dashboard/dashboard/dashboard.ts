import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardModule } from 'primeng/card';
import { MessageModule } from 'primeng/message';
import { catchError, forkJoin, of } from 'rxjs';
import { LinkService } from '../../../core/services/link.service';
import { DomainService } from '../../../core/services/domain.service';
import { UpdateService } from '../../../core/services/update.service';
import packageJson from '../../../../../package.json';

@Component({
   selector: 'app-dashboard',
   standalone: true,
   imports: [CommonModule, CardModule, MessageModule],
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
         <p-message severity="warn" [text]="loadError"></p-message>
       </div>

       <div class="col-span-full" *ngIf="showUpdateMessage">
         <p-message severity="info">
           <ng-template pTemplate>
             A new version of OpenShort is available: v{{ latestVersion }}. You are currently running v{{ currentVersion }}.
           </ng-template>
         </p-message>
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
   currentVersion = packageJson.version;
   latestVersion: string | null = null;
   showUpdateMessage = false;
   totalLinks = 0;
   activeDomains = 0;
   loading = true;
   loadError = '';

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

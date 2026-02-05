import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardModule } from 'primeng/card';
import { LinkService } from '../../../core/services/link.service';
import { DomainService } from '../../../core/services/domain.service';

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
   totalLinks = 0;
   activeDomains = 0;
   loading = true;

   constructor(
      private linkService: LinkService,
      private domainService: DomainService,
      private cdr: ChangeDetectorRef
   ) { }

   ngOnInit() {
      this.loadStats();
   }

   loadStats() {
      this.loading = true;

      this.linkService.getAll().subscribe({
         next: (links) => {
            this.totalLinks = links.length;
            this.cdr.detectChanges();
         },
         error: (err) => console.error('Error loading links:', err)
      });

      this.domainService.getAll().subscribe({
         next: (domains) => {
            this.activeDomains = domains.filter(d => d.isActive).length;
            this.loading = false;
            this.cdr.detectChanges();
         },
         error: (err) => {
            console.error('Error loading domains:', err);
            this.loading = false;
            this.cdr.detectChanges();
         }
      });
   }
}

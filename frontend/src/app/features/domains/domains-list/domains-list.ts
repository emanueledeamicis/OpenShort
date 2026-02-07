import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TooltipModule } from 'primeng/tooltip';
import { Subscription } from 'rxjs';
import { DomainService } from '../../../core/services/domain.service';
import { Domain } from '../../../core/models/api.models';

@Component({
    selector: 'app-domains-list',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        TableModule,
        ButtonModule,
        DialogModule,
        InputTextModule,
        MessageModule,
        TooltipModule
    ],
    templateUrl: './domains-list.html',
    styleUrl: './domains-list.css'
})
export class DomainsListComponent implements OnInit, OnDestroy {
    domains: Domain[] = [];
    loading = false;
    showDialog = false;
    showDeleteConfirm = false;
    showFinalConfirm = false;
    saving = false;
    deleting = false;
    errorMessage = '';
    domainForm: FormGroup;
    selectedDomain: Domain | null = null;
    linkCount = 0;
    private subscription = new Subscription();

    constructor(
        private domainService: DomainService,
        private fb: FormBuilder,
        private cdr: ChangeDetectorRef
    ) {
        this.domainForm = this.fb.group({
            host: ['', [Validators.required, Validators.pattern(/^[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/)]]
        });
    }

    ngOnInit() {
        this.loadDomains();
    }

    ngOnDestroy() {
        this.subscription.unsubscribe();
    }

    loadDomains() {
        this.loading = true;
        this.cdr.detectChanges();

        const sub = this.domainService.getAll().subscribe({
            next: (data) => {
                this.domains = data;
                this.loading = false;
                this.cdr.detectChanges();
            },
            error: (err) => {
                console.error('Error loading domains:', err);
                this.loading = false;
                this.cdr.detectChanges();
            }
        });

        this.subscription.add(sub);
    }

    openCreateDialog() {
        this.domainForm.reset();
        this.errorMessage = '';
        this.showDialog = true;
    }

    closeDialog() {
        this.showDialog = false;
        this.errorMessage = '';
    }

    saveDomain() {
        if (this.domainForm.invalid) {
            this.domainForm.markAllAsTouched();
            return;
        }

        this.saving = true;
        this.errorMessage = '';
        this.cdr.detectChanges();

        const dto = this.domainForm.value;
        const sub = this.domainService.create(dto).subscribe({
            next: () => {
                this.saving = false;
                this.closeDialog();
                this.loadDomains();
            },
            error: (err) => {
                this.saving = false;
                this.errorMessage = err.error?.detail || err.error?.title || 'Failed to create domain. Please try again.';
                this.cdr.detectChanges();
                console.error('Error creating domain:', err);
            }
        });
        this.subscription.add(sub);
    }

    // Delete - First Confirmation
    openDeleteConfirm(domain: Domain) {
        this.selectedDomain = domain;
        this.linkCount = 0;
        this.showDeleteConfirm = true;

        // Load link count
        const sub = this.domainService.getLinkCount(domain.id).subscribe({
            next: (count) => {
                this.linkCount = count;
                this.cdr.detectChanges();
            },
            error: (err) => {
                console.error('Error loading link count:', err);
            }
        });
        this.subscription.add(sub);
    }

    closeDeleteConfirm() {
        this.showDeleteConfirm = false;
        this.selectedDomain = null;
        this.linkCount = 0;
    }

    // Delete - Proceed to Final Confirmation
    proceedToFinalConfirm() {
        this.showDeleteConfirm = false;
        this.showFinalConfirm = true;
    }

    closeFinalConfirm() {
        this.showFinalConfirm = false;
        this.selectedDomain = null;
        this.linkCount = 0;
    }

    // Delete - Execute
    confirmDelete() {
        if (!this.selectedDomain) return;

        this.deleting = true;
        const sub = this.domainService.delete(this.selectedDomain.id).subscribe({
            next: () => {
                this.deleting = false;
                this.closeFinalConfirm();
                this.loadDomains();
            },
            error: (err) => {
                this.deleting = false;
                console.error('Error deleting domain:', err);
            }
        });
        this.subscription.add(sub);
    }
}

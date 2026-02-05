import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
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
        MessageModule
    ],
    templateUrl: './domains-list.html',
    styleUrl: './domains-list.css'
})
export class DomainsListComponent implements OnInit, OnDestroy {
    domains: Domain[] = [];
    loading = false;
    showDialog = false;
    saving = false;
    errorMessage = '';
    domainForm: FormGroup;
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

    deleteDomain(id: number) {
        if (confirm('Are you sure you want to delete this domain?')) {
            const sub = this.domainService.delete(id).subscribe({
                next: () => this.loadDomains(),
                error: (err) => console.error('Error deleting domain:', err)
            });
            this.subscription.add(sub);
        }
    }
}

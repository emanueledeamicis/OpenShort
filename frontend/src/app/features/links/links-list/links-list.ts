import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { Subscription } from 'rxjs';
import { LinkService } from '../../../core/services/link.service';
import { DomainService } from '../../../core/services/domain.service';
import { Link, Domain } from '../../../core/models/api.models';

@Component({
    selector: 'app-links-list',
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
    templateUrl: './links-list.html',
    styleUrl: './links-list.css'
})
export class LinksListComponent implements OnInit, OnDestroy {
    links: Link[] = [];
    domains: Domain[] = [];
    loading = false;
    showDialog = false;
    saving = false;
    errorMessage = '';
    linkForm: FormGroup;
    private subscription = new Subscription();

    constructor(
        private linkService: LinkService,
        private domainService: DomainService,
        private fb: FormBuilder,
        private cdr: ChangeDetectorRef
    ) {
        this.linkForm = this.fb.group({
            destinationUrl: ['', [Validators.required, Validators.pattern(/^https?:\/\/.+/)]],
            slug: [''],
            domain: ['', Validators.required],
            title: [''],
            notes: ['']
        });
    }

    ngOnInit() {
        this.loadLinks();
        this.loadDomains();
    }

    ngOnDestroy() {
        this.subscription.unsubscribe();
    }

    loadLinks() {
        this.loading = true;
        this.cdr.detectChanges();

        const sub = this.linkService.getAll().subscribe({
            next: (data) => {
                this.links = data;
                this.loading = false;
                this.cdr.detectChanges();
            },
            error: (err) => {
                console.error('Error loading links:', err);
                this.loading = false;
                this.cdr.detectChanges();
            }
        });

        this.subscription.add(sub);
    }

    loadDomains() {
        const sub = this.domainService.getAll().subscribe({
            next: (data) => {
                this.domains = data;
                this.cdr.detectChanges();
            },
            error: (err) => console.error('Error loading domains:', err)
        });
        this.subscription.add(sub);
    }

    openCreateDialog() {
        this.linkForm.reset();
        this.errorMessage = '';
        this.showDialog = true;
    }

    closeDialog() {
        this.showDialog = false;
        this.errorMessage = '';
    }

    saveLink() {
        if (this.linkForm.invalid) {
            this.linkForm.markAllAsTouched();
            return;
        }

        this.saving = true;
        this.errorMessage = '';
        this.cdr.detectChanges();

        const dto = this.linkForm.value;
        const sub = this.linkService.create(dto).subscribe({
            next: () => {
                this.saving = false;
                this.closeDialog();
                this.loadLinks();
            },
            error: (err) => {
                this.saving = false;
                this.errorMessage = err.error?.detail || err.error?.title || 'Failed to create link. Please try again.';
                this.cdr.detectChanges();
                console.error('Error creating link:', err);
            }
        });
        this.subscription.add(sub);
    }

    deleteLink(id: number) {
        if (confirm('Are you sure you want to delete this link?')) {
            const sub = this.linkService.delete(id).subscribe({
                next: () => this.loadLinks(),
                error: (err) => console.error('Error deleting link:', err)
            });
            this.subscription.add(sub);
        }
    }
}

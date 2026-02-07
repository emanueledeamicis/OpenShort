import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { Subscription } from 'rxjs';
import { LinkService } from '../../../core/services/link.service';
import { DomainService } from '../../../core/services/domain.service';
import { Link, Domain, RedirectType, UpdateLinkDto } from '../../../core/models/api.models';

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
        MessageModule,
        CheckboxModule,
        SelectModule,
        TooltipModule
    ],
    templateUrl: './links-list.html',
    styleUrl: './links-list.css'
})
export class LinksListComponent implements OnInit, OnDestroy {
    links: Link[] = [];
    domains: Domain[] = [];
    loading = false;
    showDialog = false;
    showEditDialog = false;
    showDeleteConfirm = false;
    showToggleConfirm = false;
    saving = false;
    errorMessage = '';
    editErrorMessage = '';
    selectedLink: Link | null = null;
    linkForm: FormGroup;
    editForm: FormGroup;
    private subscription = new Subscription();

    redirectTypeOptions = [
        { label: 'Permanent (301)', value: RedirectType.Permanent },
        { label: 'Temporary (302)', value: RedirectType.Temporary }
    ];

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
            notes: [''],
            isActive: [true]
        });

        this.editForm = this.fb.group({
            destinationUrl: ['', [Validators.required, Validators.pattern(/^https?:\/\/.+/)]],
            redirectType: [RedirectType.Permanent, Validators.required],
            title: [''],
            notes: [''],
            isActive: [true]
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

    // Create Dialog
    openCreateDialog() {
        this.linkForm.reset({ isActive: true });
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

    // Edit Dialog
    openEditDialog(link: Link) {
        this.selectedLink = link;
        this.editForm.patchValue({
            destinationUrl: link.destinationUrl,
            redirectType: link.redirectType,
            title: link.title || '',
            notes: link.notes || '',
            isActive: link.isActive
        });
        this.editErrorMessage = '';
        this.showEditDialog = true;
    }

    closeEditDialog() {
        this.showEditDialog = false;
        this.editErrorMessage = '';
        this.selectedLink = null;
    }

    updateLink() {
        if (this.editForm.invalid || !this.selectedLink) {
            this.editForm.markAllAsTouched();
            return;
        }

        this.saving = true;
        this.editErrorMessage = '';
        this.cdr.detectChanges();

        const dto: UpdateLinkDto = this.editForm.value;
        const sub = this.linkService.update(this.selectedLink.id!, dto).subscribe({
            next: () => {
                this.saving = false;
                this.closeEditDialog();
                this.loadLinks();
            },
            error: (err) => {
                this.saving = false;
                this.editErrorMessage = err.error?.detail || err.error?.title || 'Failed to update link. Please try again.';
                this.cdr.detectChanges();
                console.error('Error updating link:', err);
            }
        });
        this.subscription.add(sub);
    }

    // Delete Confirmation
    openDeleteConfirm(link: Link) {
        this.selectedLink = link;
        this.showDeleteConfirm = true;
    }

    closeDeleteConfirm() {
        this.showDeleteConfirm = false;
        this.selectedLink = null;
    }

    confirmDelete() {
        if (!this.selectedLink) return;

        this.saving = true;
        const sub = this.linkService.delete(this.selectedLink.id!).subscribe({
            next: () => {
                this.saving = false;
                this.closeDeleteConfirm();
                this.loadLinks();
            },
            error: (err) => {
                this.saving = false;
                console.error('Error deleting link:', err);
            }
        });
        this.subscription.add(sub);
    }

    // Toggle Confirmation
    openToggleConfirm(link: Link) {
        this.selectedLink = link;
        this.showToggleConfirm = true;
    }

    closeToggleConfirm() {
        this.showToggleConfirm = false;
        this.selectedLink = null;
    }

    confirmToggle() {
        if (!this.selectedLink) return;

        this.saving = true;
        const dto: UpdateLinkDto = {
            destinationUrl: this.selectedLink.destinationUrl,
            redirectType: this.selectedLink.redirectType,
            title: this.selectedLink.title,
            notes: this.selectedLink.notes,
            isActive: !this.selectedLink.isActive
        };

        const sub = this.linkService.update(this.selectedLink.id!, dto).subscribe({
            next: () => {
                this.saving = false;
                this.closeToggleConfirm();
                this.loadLinks();
            },
            error: (err) => {
                this.saving = false;
                console.error('Error toggling link status:', err);
            }
        });
        this.subscription.add(sub);
    }
}

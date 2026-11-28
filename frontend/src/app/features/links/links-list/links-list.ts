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
import { RouterLink } from '@angular/router';
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
        TooltipModule,
        RouterLink
    ],
    templateUrl: './links-list.html',
    styleUrl: './links-list.css'
})
export class LinksListComponent implements OnInit, OnDestroy {
    private static readonly loadLinksErrorMessage = 'Unable to load links.';
    private static readonly loadDomainsErrorMessage = 'Unable to load domains.';
    private static readonly createLinkErrorMessage = 'Failed to create link. Please try again.';
    private static readonly updateLinkErrorMessage = 'Failed to update link. Please try again.';
    private static readonly deleteLinkErrorMessage = 'Failed to delete link. Please try again.';
    private static readonly toggleLinkErrorMessage = 'Failed to update link status. Please try again.';
    private static readonly copyLinkErrorMessage = 'Unable to copy the short URL.';

    links: Link[] = [];
    domains: Domain[] = [];
    // The ones a new link can be created on: the API refuses a domain that is turned off
    activeDomains: Domain[] = [];
    // False until the domains have arrived once: an empty list then means that there are none
    domainsLoaded = false;
    loading = false;
    showDialog = false;
    showEditDialog = false;
    showDeleteConfirm = false;
    showToggleConfirm = false;
    saving = false;
    pageErrorMessage = '';
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
            // Same rules as the API: one segment of the path of the short URL
            slug: ['', [Validators.pattern(/^[A-Za-z0-9_-]*$/), Validators.maxLength(50)]],
            domain: ['', Validators.required],
            redirectType: [RedirectType.Permanent, Validators.required],
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
        this.pageErrorMessage = '';
        this.cdr.detectChanges();

        const sub = this.linkService.getAll().subscribe({
            next: (data) => {
                this.links = data;
                this.loading = false;
                this.cdr.detectChanges();
            },
            error: (err) => {
                this.pageErrorMessage = this.extractApiErrorMessage(err, LinksListComponent.loadLinksErrorMessage);
                this.loading = false;
                this.cdr.detectChanges();
            }
        });

        this.subscription.add(sub);
    }

    loadDomains() {
        const sub = this.domainService.getAll().subscribe({
            next: (data) => {
                // Sort by ID descending (newest first)
                this.domains = data.sort((a, b) => b.id - a.id);
                this.activeDomains = this.domains.filter((domain) => domain.isActive);
                this.domainsLoaded = true;
                this.selectDefaultDomain();
                this.cdr.detectChanges();
            },
            error: (err) => {
                // In the form when it is open: the page is behind it
                const message = this.extractApiErrorMessage(err, LinksListComponent.loadDomainsErrorMessage);
                if (this.showDialog) {
                    this.errorMessage = message;
                } else {
                    this.pageErrorMessage = message;
                }
                this.cdr.detectChanges();
            }
        });
        this.subscription.add(sub);
    }

    // In the form of a new link, selects the last created domain that is active, unless the one
    // already chosen still is. The domains may arrive, or change, while the form is open
    private selectDefaultDomain() {
        if (!this.showDialog) {
            return;
        }

        const selected = this.linkForm.get('domain')?.value;
        if (!this.activeDomains.some((domain) => domain.host === selected)) {
            this.linkForm.patchValue({ domain: this.activeDomains.length > 0 ? this.activeDomains[0].host : '' });
        }
    }

    // Create Dialog
    openCreateDialog() {
        this.linkForm.reset({
            isActive: true,
            domain: '',
            redirectType: RedirectType.Permanent
        });
        this.errorMessage = '';
        this.pageErrorMessage = '';
        this.showDialog = true;
        this.selectDefaultDomain();

        // A domain may have been turned on or off since the page was opened
        this.loadDomains();
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

        // Lowercase the slug if provided
        if (dto.slug) {
            dto.slug = dto.slug.toLowerCase();
        }

        // Lowercase protocol and host of the destination URL
        try {
            if (dto.destinationUrl) {
                const urlObj = new URL(dto.destinationUrl);
                urlObj.protocol = urlObj.protocol.toLowerCase();
                urlObj.hostname = urlObj.hostname.toLowerCase();
                dto.destinationUrl = urlObj.toString();
            }
        } catch (e) { /* Let backend or regex catch invalid URLs */ }

        const sub = this.linkService.create(dto).subscribe({
            next: () => {
                this.saving = false;
                this.closeDialog();
                this.loadLinks();
            },
            error: (err) => {
                this.saving = false;
                this.errorMessage = this.extractApiErrorMessage(err, LinksListComponent.createLinkErrorMessage);
                this.cdr.detectChanges();
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

        // Lowercase protocol and host of the destination URL
        try {
            if (dto.destinationUrl) {
                const urlObj = new URL(dto.destinationUrl);
                urlObj.protocol = urlObj.protocol.toLowerCase();
                urlObj.hostname = urlObj.hostname.toLowerCase();
                dto.destinationUrl = urlObj.toString();
            }
        } catch (e) { /* Let backend or regex catch invalid URLs */ }

        const sub = this.linkService.update(this.selectedLink.id, dto).subscribe({
            next: () => {
                this.saving = false;
                this.closeEditDialog();
                this.loadLinks();
            },
            error: (err) => {
                this.saving = false;
                this.editErrorMessage = this.extractApiErrorMessage(err, LinksListComponent.updateLinkErrorMessage);
                this.cdr.detectChanges();
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
        // A second click while the request is running must not send it again
        if (!this.selectedLink || this.saving) return;

        this.saving = true;
        this.cdr.detectChanges();
        const sub = this.linkService.delete(this.selectedLink.id).subscribe({
            next: () => {
                this.saving = false;
                this.closeDeleteConfirm();
                this.loadLinks();
            },
            error: (err) => {
                this.saving = false;
                this.pageErrorMessage = this.extractApiErrorMessage(err, LinksListComponent.deleteLinkErrorMessage);
                this.cdr.detectChanges();
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
        // A second click while the request is running must not send it again
        if (!this.selectedLink || this.saving) return;

        this.saving = true;
        this.cdr.detectChanges();
        const dto: UpdateLinkDto = {
            destinationUrl: this.selectedLink.destinationUrl,
            redirectType: this.selectedLink.redirectType,
            title: this.selectedLink.title,
            notes: this.selectedLink.notes,
            isActive: !this.selectedLink.isActive
        };

        const sub = this.linkService.update(this.selectedLink.id, dto).subscribe({
            next: () => {
                this.saving = false;
                this.closeToggleConfirm();
                this.loadLinks();
            },
            error: (err) => {
                this.saving = false;
                this.pageErrorMessage = this.extractApiErrorMessage(err, LinksListComponent.toggleLinkErrorMessage);
                this.cdr.detectChanges();
            }
        });
        this.subscription.add(sub);
    }

    copyToClipboard(url: string) {
        navigator.clipboard.writeText(url).catch(() => {
            this.pageErrorMessage = LinksListComponent.copyLinkErrorMessage;
            this.cdr.detectChanges();
        });
    }

    private extractApiErrorMessage(err: any, fallbackMessage: string): string {
        const apiError = err?.error;
        const validationErrors = apiError?.errors;

        if (validationErrors && typeof validationErrors === 'object') {
            const messages = Object.values(validationErrors)
                .flatMap((value) => Array.isArray(value) ? value : [value])
                .filter((value): value is string => typeof value === 'string' && value.trim().length > 0);

            if (messages.length > 0) {
                return messages.join(' ');
            }
        }

        return apiError?.message || apiError?.detail || apiError?.title || fallbackMessage;
    }
}

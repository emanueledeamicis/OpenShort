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
import { Domain, DomainNotFoundBehavior, UpdateDomainDto } from '../../../core/models/api.models';

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
    private static readonly loadDomainsErrorMessage = 'Unable to load domains.';
    private static readonly createDomainErrorMessage = 'Failed to create domain. Please try again.';
    private static readonly updateDomainErrorMessage = 'Failed to update domain settings. Please try again.';
    private static readonly loadLinkCountErrorMessage = 'Unable to load the number of links for this domain.';
    private static readonly deleteDomainErrorMessage = 'Failed to delete domain. Please try again.';

    domains: Domain[] = [];
    loading = false;
    showDialog = false;
    showSettingsDialog = false;
    showDeleteConfirm = false;
    showFinalConfirm = false;
    saving = false;
    savingSettings = false;
    deleting = false;
    pageErrorMessage = '';
    errorMessage = '';
    settingsErrorMessage = '';
    domainForm: FormGroup;
    settingsForm: FormGroup;
    selectedDomain: Domain | null = null;
    linkCount = 0;
    readonly domainNotFoundBehavior = DomainNotFoundBehavior;
    private subscription = new Subscription();

    constructor(
        private domainService: DomainService,
        private fb: FormBuilder,
        private cdr: ChangeDetectorRef
    ) {
        this.domainForm = this.fb.group({
            host: ['', [Validators.required, Validators.pattern(/^[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/)]]
        });

        this.settingsForm = this.fb.group({
            isActive: [true],
            notFoundBehavior: [DomainNotFoundBehavior.OpenShortPage, Validators.required],
            notFoundRedirectUrl: ['']
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
        this.pageErrorMessage = '';
        this.cdr.detectChanges();

        const sub = this.domainService.getAll().subscribe({
            next: (data) => {
                this.domains = data;
                this.loading = false;
                this.cdr.detectChanges();
            },
            error: (err) => {
                this.pageErrorMessage = this.extractApiErrorMessage(err, DomainsListComponent.loadDomainsErrorMessage);
                this.loading = false;
                this.cdr.detectChanges();
            }
        });

        this.subscription.add(sub);
    }

    openCreateDialog() {
        this.domainForm.reset();
        this.errorMessage = '';
        this.pageErrorMessage = '';
        this.showDialog = true;
    }

    closeDialog() {
        this.showDialog = false;
        this.errorMessage = '';
    }

    openSettingsDialog(domain: Domain) {
        this.selectedDomain = domain;
        this.settingsErrorMessage = '';
        this.settingsForm.reset({
            isActive: domain.isActive,
            notFoundBehavior: domain.notFoundBehavior ?? DomainNotFoundBehavior.OpenShortPage,
            notFoundRedirectUrl: domain.notFoundRedirectUrl ?? ''
        });
        this.showSettingsDialog = true;
    }

    closeSettingsDialog() {
        this.showSettingsDialog = false;
        this.settingsErrorMessage = '';
        this.selectedDomain = null;
    }

    saveDomainSettings() {
        if (!this.selectedDomain) {
            return;
        }

        const dto = this.buildUpdateDomainDto();
        if (!dto) {
            return;
        }

        this.savingSettings = true;
        this.settingsErrorMessage = '';
        this.cdr.detectChanges();

        const sub = this.domainService.update(this.selectedDomain.id, dto).subscribe({
            next: () => {
                this.savingSettings = false;
                this.closeSettingsDialog();
                this.loadDomains();
            },
            error: (err) => {
                this.savingSettings = false;
                this.settingsErrorMessage = this.extractApiErrorMessage(err, DomainsListComponent.updateDomainErrorMessage);
                this.cdr.detectChanges();
            }
        });

        this.subscription.add(sub);
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
        if (dto.host) {
            dto.host = dto.host.toLowerCase();
        }

        const sub = this.domainService.create(dto).subscribe({
            next: () => {
                this.saving = false;
                this.closeDialog();
                this.loadDomains();
            },
            error: (err) => {
                this.saving = false;
                this.errorMessage = this.extractApiErrorMessage(err, DomainsListComponent.createDomainErrorMessage);
                this.cdr.detectChanges();
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
                this.pageErrorMessage = this.extractApiErrorMessage(err, DomainsListComponent.loadLinkCountErrorMessage);
                this.cdr.detectChanges();
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
                this.pageErrorMessage = this.extractApiErrorMessage(err, DomainsListComponent.deleteDomainErrorMessage);
                this.cdr.detectChanges();
            }
        });
        this.subscription.add(sub);
    }

    requiresCustomUrl(): boolean {
        return this.settingsForm.get('notFoundBehavior')?.value === DomainNotFoundBehavior.RedirectToCustomUrl;
    }

    private buildUpdateDomainDto(): UpdateDomainDto | null {
        const notFoundBehavior = this.settingsForm.get('notFoundBehavior')?.value as DomainNotFoundBehavior;
        const rawRedirectUrl = this.settingsForm.get('notFoundRedirectUrl')?.value as string | null;
        const notFoundRedirectUrl = rawRedirectUrl?.trim() || null;

        if (notFoundBehavior === DomainNotFoundBehavior.RedirectToCustomUrl) {
            const isValidUrl = !!notFoundRedirectUrl && /^https?:\/\/.+/i.test(notFoundRedirectUrl);
            if (!isValidUrl) {
                this.settingsErrorMessage = 'Enter a valid absolute URL for the custom 404 redirect.';
                this.cdr.detectChanges();
                return null;
            }
        }

        return {
            isActive: !!this.settingsForm.get('isActive')?.value,
            notFoundBehavior,
            notFoundRedirectUrl: notFoundBehavior === DomainNotFoundBehavior.RedirectToCustomUrl ? notFoundRedirectUrl : null
        };
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

import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormGroup, FormBuilder, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SecurityService, ApiKeyInfo } from '../../core/services/security.service';

@Component({
    selector: 'app-security',
    standalone: true,
    imports: [CommonModule, FormsModule, ReactiveFormsModule, ButtonModule, InputTextModule, MessageModule],
    templateUrl: './security.html'
})
export class SecurityComponent implements OnInit {
    // API Key
    apiKeyInfo: ApiKeyInfo | null = null;
    generatedKey: string | null = null;
    isLoadingApiKey = true;
    isGeneratingApiKey = false;
    showConfirmRegenerate = false;
    keyCopied = false;

    // Password
    passwordForm: FormGroup;
    isChangingPassword = false;
    passwordMessage: string | null = null;
    passwordError: string | null = null;

    constructor(
        private securityService: SecurityService,
        private fb: FormBuilder,
        private cdr: ChangeDetectorRef
    ) {
        this.passwordForm = this.fb.group({
            currentPassword: ['', Validators.required],
            newPassword: ['', [Validators.required, Validators.minLength(6)]],
            confirmPassword: ['', Validators.required]
        });
    }

    ngOnInit() {
        this.loadApiKeyInfo();
    }

    loadApiKeyInfo() {
        this.isLoadingApiKey = true;
        console.log('[Security] Starting API key load...');
        this.securityService.getApiKeyInfo().subscribe({
            next: (info) => {
                console.log('[Security] Received API key info:', info);
                this.apiKeyInfo = info;
                this.isLoadingApiKey = false;
                this.cdr.detectChanges(); // Force UI update
            },
            error: (err) => {
                console.error('[Security] Error loading API key:', err);
                this.isLoadingApiKey = false;
                this.cdr.detectChanges(); // Force UI update
            },
            complete: () => {
                console.log('[Security] Observable completed');
            }
        });
    }

    promptGenerateApiKey() {
        if (this.apiKeyInfo?.exists) {
            this.showConfirmRegenerate = true;
        } else {
            this.generateApiKey();
        }
    }

    generateApiKey() {
        this.showConfirmRegenerate = false;
        this.isGeneratingApiKey = true;
        this.generatedKey = null;

        this.securityService.generateApiKey().subscribe({
            next: (result) => {
                console.log('[Security] Generated API key:', result);
                this.generatedKey = result.key;
                this.apiKeyInfo = {
                    exists: true,
                    prefix: result.prefix,
                    createdAt: result.createdAt
                };
                this.isGeneratingApiKey = false;
                this.cdr.detectChanges(); // Force UI update
            },
            error: (err) => {
                console.error('[Security] Error generating API key:', err);
                this.isGeneratingApiKey = false;
                this.cdr.detectChanges(); // Force UI update
            }
        });
    }

    cancelRegenerate() {
        this.showConfirmRegenerate = false;
    }

    copyKey() {
        if (this.generatedKey) {
            navigator.clipboard.writeText(this.generatedKey);
            this.keyCopied = true;
            setTimeout(() => this.keyCopied = false, 2000);
        }
    }

    dismissGeneratedKey() {
        this.generatedKey = null;
    }

    changePassword() {
        if (this.passwordForm.invalid) return;

        const formValue = this.passwordForm.value;
        if (formValue.newPassword !== formValue.confirmPassword) {
            this.passwordError = 'Passwords do not match.';
            return;
        }

        this.isChangingPassword = true;
        this.passwordMessage = null;
        this.passwordError = null;

        this.securityService.changePassword({
            currentPassword: formValue.currentPassword,
            newPassword: formValue.newPassword,
            confirmPassword: formValue.confirmPassword
        }).subscribe({
            next: (result) => {
                this.passwordMessage = 'Password changed successfully!';
                this.passwordForm.reset();
                this.isChangingPassword = false;
                this.cdr.detectChanges(); // Force UI update
            },
            error: (err) => {
                this.passwordError = err.error?.message || 'Error occurred while changing password.';
                this.isChangingPassword = false;
                this.cdr.detectChanges(); // Force UI update
            }
        });
    }
}

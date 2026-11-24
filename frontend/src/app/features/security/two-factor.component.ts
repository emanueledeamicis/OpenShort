import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable, Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import qrcode from 'qrcode-generator';
import {
    SecurityService,
    TwoFactorRecoveryCodes,
    TwoFactorSetup,
    TwoFactorStatus
} from '../../core/services/security.service';

// What the password is being asked for
type PasswordAction = 'setup' | 'recovery-codes' | 'disable';

@Component({
    selector: 'app-two-factor',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, MessageModule],
    templateUrl: './two-factor.html'
})
export class TwoFactorComponent implements OnInit, OnDestroy {
    private static readonly loadErrorMessage = 'Unable to load the two-factor authentication status.';
    private static readonly unexpectedErrorMessage = 'Something went wrong. Please try again.';
    private static readonly copyErrorMessage = 'The codes could not be copied. Copy them by hand.';
    private static readonly conflictStatus = 409;

    // Empty modules around the code, which a QR code needs to be read
    private static readonly qrQuietZone = 4;

    status: TwoFactorStatus | null = null;
    isLoading = true;
    isBusy = false;
    errorMessage: string | null = null;
    successMessage: string | null = null;

    passwordAction: PasswordAction | null = null;
    password = '';

    // Set between the setup and the confirmation of the first code
    setup: TwoFactorSetup | null = null;
    qrPath = '';
    qrViewBox = '';
    code = '';

    // Shown once, right after they are generated
    recoveryCodes: string[] | null = null;
    codesCopied = false;

    private subscription = new Subscription();
    private copiedTimeout: ReturnType<typeof setTimeout> | null = null;

    constructor(
        private securityService: SecurityService,
        private cdr: ChangeDetectorRef
    ) { }

    ngOnInit() {
        this.loadStatus();
    }

    ngOnDestroy() {
        this.subscription.unsubscribe();
        if (this.copiedTimeout) {
            clearTimeout(this.copiedTimeout);
        }
    }

    get formattedKey(): string {
        return this.setup?.sharedKey.match(/.{1,4}/g)?.join(' ') ?? '';
    }

    get passwordPrompt(): string {
        switch (this.passwordAction) {
            case 'setup':
                return 'Enter your password to set up two-factor authentication.';
            case 'recovery-codes':
                return 'Enter your password to generate new recovery codes. The current ones stop working.';
            case 'disable':
                return 'Enter your password to turn off two-factor authentication.';
            default:
                return '';
        }
    }

    get passwordActionLabel(): string {
        switch (this.passwordAction) {
            case 'recovery-codes':
                return 'Generate new codes';
            case 'disable':
                return 'Turn off';
            default:
                return 'Continue';
        }
    }

    // The message of a failed action is kept when the status is loaded again because of it
    loadStatus(keepErrorMessage = false) {
        this.isLoading = true;
        if (!keepErrorMessage) {
            this.errorMessage = null;
        }

        this.cdr.detectChanges();

        this.subscription.add(this.securityService.getTwoFactorStatus().subscribe({
            next: (status) => {
                this.status = status;
                this.isLoading = false;
                this.cdr.detectChanges();
            },
            error: () => {
                this.status = null;
                this.errorMessage = TwoFactorComponent.loadErrorMessage;
                this.isLoading = false;
                this.cdr.detectChanges();
            }
        }));
    }

    askPassword(action: PasswordAction) {
        this.passwordAction = action;
        this.password = '';
        this.errorMessage = null;
        this.successMessage = null;
    }

    cancel() {
        this.closeForms();
        this.errorMessage = null;
    }

    submitPassword() {
        if (!this.password || !this.passwordAction) {
            return;
        }

        switch (this.passwordAction) {
            case 'setup':
                this.run(this.securityService.setupTwoFactor(this.password), (setup) => this.showSetup(setup));
                break;
            case 'recovery-codes':
                this.run(this.securityService.generateRecoveryCodes(this.password), (result) => {
                    this.showRecoveryCodes(result);
                });
                break;
            case 'disable':
                this.run(this.securityService.disableTwoFactor(this.password), () => {
                    this.status = { isEnabled: false, recoveryCodesLeft: 0 };
                    this.successMessage = 'Two-factor authentication has been turned off.';
                });
                break;
        }
    }

    confirmCode() {
        if (!this.code.trim()) {
            return;
        }

        this.run(this.securityService.enableTwoFactor(this.code.trim()), (result) => {
            this.closeForms();
            this.successMessage = 'Two-factor authentication is on.';
            this.showRecoveryCodes(result);
        });
    }

    // The clipboard is missing on a page served over plain HTTP, and the browser can refuse the copy
    copyRecoveryCodes() {
        if (!this.recoveryCodes) {
            return;
        }

        const copy = navigator.clipboard
            ? navigator.clipboard.writeText(this.recoveryCodes.join('\n'))
            : Promise.reject();

        copy.then(
            () => {
                this.codesCopied = true;
                this.errorMessage = null;
                this.cdr.detectChanges();
                this.copiedTimeout = setTimeout(() => {
                    this.codesCopied = false;
                    this.cdr.detectChanges();
                }, 2000);
            },
            () => {
                this.errorMessage = TwoFactorComponent.copyErrorMessage;
                this.cdr.detectChanges();
            }
        );
    }

    dismissRecoveryCodes() {
        this.recoveryCodes = null;
        this.successMessage = null;
        this.errorMessage = null;
    }

    private showSetup(setup: TwoFactorSetup) {
        this.setup = setup;
        this.code = '';
        this.drawQrCode(setup.authenticatorUri);
    }

    private showRecoveryCodes(result: TwoFactorRecoveryCodes) {
        this.recoveryCodes = result.recoveryCodes;
        this.status = { isEnabled: true, recoveryCodesLeft: result.recoveryCodes.length };
    }

    // Also forgets the secret: the QR code is enough to read it back
    private closeForms() {
        this.passwordAction = null;
        this.password = '';
        this.setup = null;
        this.qrPath = '';
        this.qrViewBox = '';
        this.code = '';
    }

    // The code is drawn as one SVG path, a square for each dark module
    private drawQrCode(text: string) {
        const qr = qrcode(0, 'M');
        qr.addData(text);
        qr.make();

        const size = qr.getModuleCount();
        let path = '';
        for (let row = 0; row < size; row++) {
            for (let column = 0; column < size; column++) {
                if (qr.isDark(row, column)) {
                    path += `M${column} ${row}h1v1h-1z`;
                }
            }
        }

        const margin = TwoFactorComponent.qrQuietZone;
        this.qrPath = path;
        this.qrViewBox = `${-margin} ${-margin} ${size + margin * 2} ${size + margin * 2}`;
    }

    // Runs a call that needed the password or the code, which are forgotten when it succeeds
    private run<T>(call: Observable<T>, onSuccess: (result: T) => void) {
        this.isBusy = true;
        this.errorMessage = null;
        this.successMessage = null;

        this.subscription.add(call.subscribe({
            next: (result) => {
                this.isBusy = false;
                this.passwordAction = null;
                this.password = '';
                onSuccess(result);
                this.cdr.detectChanges();
            },
            error: (err) => {
                this.isBusy = false;
                this.errorMessage = this.extractApiErrorMessage(err);

                // The page is behind: two-factor authentication was turned on or off somewhere else,
                // or an earlier answer was lost. The form no longer applies
                if (err?.status === TwoFactorComponent.conflictStatus) {
                    this.closeForms();
                    this.loadStatus(true);
                    return;
                }

                this.cdr.detectChanges();
            }
        }));
    }

    private extractApiErrorMessage(err: any): string {
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

        return apiError?.message || apiError?.detail || TwoFactorComponent.unexpectedErrorMessage;
    }
}

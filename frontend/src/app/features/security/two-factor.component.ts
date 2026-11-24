import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
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
export class TwoFactorComponent implements OnInit {
    private static readonly loadErrorMessage = 'Unable to load the two-factor authentication status.';
    private static readonly unexpectedErrorMessage = 'Something went wrong. Please try again.';

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

    constructor(
        private securityService: SecurityService,
        private cdr: ChangeDetectorRef
    ) { }

    ngOnInit() {
        this.loadStatus();
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

    loadStatus() {
        this.isLoading = true;
        this.securityService.getTwoFactorStatus().subscribe({
            next: (status) => {
                this.status = status;
                this.isLoading = false;
                this.cdr.detectChanges();
            },
            error: () => {
                this.errorMessage = TwoFactorComponent.loadErrorMessage;
                this.isLoading = false;
                this.cdr.detectChanges();
            }
        });
    }

    askPassword(action: PasswordAction) {
        this.passwordAction = action;
        this.password = '';
        this.errorMessage = null;
        this.successMessage = null;
    }

    cancel() {
        this.passwordAction = null;
        this.password = '';
        this.setup = null;
        this.code = '';
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
            this.setup = null;
            this.code = '';
            this.successMessage = 'Two-factor authentication is on.';
            this.showRecoveryCodes(result);
        });
    }

    copyRecoveryCodes() {
        if (this.recoveryCodes) {
            navigator.clipboard.writeText(this.recoveryCodes.join('\n'));
            this.codesCopied = true;
            setTimeout(() => {
                this.codesCopied = false;
                this.cdr.detectChanges();
            }, 2000);
        }
    }

    dismissRecoveryCodes() {
        this.recoveryCodes = null;
        this.successMessage = null;
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

        call.subscribe({
            next: (result) => {
                this.isBusy = false;
                this.passwordAction = null;
                this.password = '';
                onSuccess(result);
                this.cdr.detectChanges();
            },
            error: (err) => {
                this.isBusy = false;
                this.errorMessage = err?.error?.message || err?.error?.detail || TwoFactorComponent.unexpectedErrorMessage;
                this.cdr.detectChanges();
            }
        });
    }
}

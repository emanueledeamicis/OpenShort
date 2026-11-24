import { Component, ChangeDetectorRef, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../../core/services/auth.service';
import packageJson from '../../../../../package.json';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, CardModule, InputTextModule, ButtonModule, MessageModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class LoginComponent implements OnInit {
  version = packageJson.version;
  identifier = '';
  password = '';
  confirmPassword = '';

  // Second step of the login, for an account with two-factor authentication: the password was right
  isCodeRequired = false;
  code = '';
  setupUserName = '';
  loading = false;
  loadingStatus = true;
  isInitialSetupRequired = false;
  errorMessage = '';

  constructor(
    private authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit() {
    this.authService.getInitialSetupStatus().subscribe({
      next: (status) => {
        this.isInitialSetupRequired = status.isSetupRequired;
        this.setupUserName = status.userName;
        this.loadingStatus = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.loadingStatus = false;
        this.errorMessage = this.extractSetupStatusErrorMessage(err);
        this.cdr.detectChanges();
      }
    });
  }

  onLogin() {
    if (!this.identifier || !this.password || (this.isCodeRequired && !this.code.trim())) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    // There is no state on the server between the two steps: the same credentials are sent again with the code
    const code = this.isCodeRequired ? this.code.trim() : undefined;

    this.authService.login(this.identifier, this.password, code).subscribe({
      next: () => {
        this.loading = false;
        this.cdr.detectChanges();
        const redirect = this.authService.redirectUrl || '/dashboard';
        this.authService.redirectUrl = null;
        this.router.navigate([redirect]);
      },
      error: (err) => {
        this.loading = false;

        if (err?.status === 401 && err?.error?.requiresTwoFactor === true) {
          this.askForCode(err);
        } else {
          // A 401 here means that the password is no longer accepted: back to the first step
          if (err?.status === 401 && this.isCodeRequired) {
            this.backToPassword();
          }

          this.errorMessage = this.extractApiErrorMessage(err, 'Login failed. Please check your credentials.');
        }

        this.cdr.detectChanges();
      }
    });
  }

  backToPassword() {
    this.isCodeRequired = false;
    this.code = '';
    this.password = '';
    this.errorMessage = '';
  }

  private askForCode(err: any) {
    // The first answer only asks for the code; from the second step on it means that the code was wrong
    if (this.isCodeRequired) {
      this.errorMessage = this.extractApiErrorMessage(err, 'The code is not valid.');
    }

    this.isCodeRequired = true;
    this.code = '';

    // The field exists only after the view is updated
    setTimeout(() => document.getElementById('code')?.focus());
  }

  onSetupAdmin() {
    if (!this.password || !this.confirmPassword) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    this.authService.setupAdmin(this.password, this.confirmPassword).subscribe({
      next: () => {
        this.loading = false;
        this.isInitialSetupRequired = false;
        this.cdr.detectChanges();
        this.authService.redirectUrl = null;
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = this.extractApiErrorMessage(err, 'Unable to complete the initial admin setup.');
        this.cdr.detectChanges();
      }
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

    return apiError?.message || apiError?.detail || fallbackMessage;
  }

  private extractSetupStatusErrorMessage(err: any): string {
    const status = err?.status;
    const apiError = err?.error;
    const detail = apiError?.detail || apiError?.message;

    if (typeof detail === 'string' && detail.trim().length > 0) {
      return detail;
    }

    if (status === 0) {
      return 'Unable to reach the OpenShort API. Verify that the container is running and the backend is reachable.';
    }

    if (status >= 500) {
      return 'Unable to load authentication status. The backend may have failed to start. Check the container logs, especially the database connection settings.';
    }

    return 'Unable to load authentication status.';
  }
}

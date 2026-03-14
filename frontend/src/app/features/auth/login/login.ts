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
      error: () => {
        this.loadingStatus = false;
        this.errorMessage = 'Unable to load authentication status.';
        this.cdr.detectChanges();
      }
    });
  }

  onLogin() {
    this.loading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    this.authService.login(this.identifier, this.password).subscribe({
      next: () => {
        this.loading = false;
        this.cdr.detectChanges();
        const redirect = this.authService.redirectUrl || '/dashboard';
        this.authService.redirectUrl = null;
        this.router.navigate([redirect]);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = 'Login failed. Please check your credentials.';
        this.cdr.detectChanges();
        console.error('Login error:', err);
      }
    });
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
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.message || err.error?.detail || 'Unable to complete the initial admin setup.';
        this.cdr.detectChanges();
        console.error('Initial setup error:', err);
      }
    });
  }
}

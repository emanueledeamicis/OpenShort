import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, CardModule, InputTextModule, ButtonModule, MessageModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class LoginComponent {
  email = '';
  password = '';
  loading = false;
  errorMessage = '';

  constructor(
    private authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) { }

  onLogin() {
    this.loading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    this.authService.login(this.email, this.password).subscribe({
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
}

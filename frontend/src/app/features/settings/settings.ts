import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { Subscription } from 'rxjs';
import { SettingsService } from '../../core/services/settings.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    InputTextModule,
    ButtonModule,
    MessageModule
  ],
  templateUrl: './settings.html',
  styleUrl: './settings.css',
})
export class Settings implements OnInit, OnDestroy {
  settingsForm: FormGroup;
  loading = false;
  saving = false;
  successMessage = '';
  errorMessage = '';
  private subscription = new Subscription();

  constructor(
    private settingsService: SettingsService,
    private fb: FormBuilder,
    private cdr: ChangeDetectorRef
  ) {
    this.settingsForm = this.fb.group({
      cacheDuration: ['', [Validators.required, Validators.min(0), Validators.max(86400)]]
    });
  }

  ngOnInit() {
    this.loadSettings();
  }

  ngOnDestroy() {
    this.subscription.unsubscribe();
  }

  loadSettings() {
    this.loading = true;
    this.cdr.detectChanges();

    const sub = this.settingsService.getSetting('CacheDurationSeconds').subscribe({
      next: (data) => {
        this.settingsForm.patchValue({
          cacheDuration: data.value
        });
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error loading setting:', err);
        // Default to 600 if not found
        this.settingsForm.patchValue({
          cacheDuration: '600'
        });
        this.loading = false;
        this.cdr.detectChanges();
      }
    });

    this.subscription.add(sub);
  }

  saveSettings() {
    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.successMessage = '';
    this.errorMessage = '';
    this.cdr.detectChanges();

    const value = this.settingsForm.value.cacheDuration.toString();
    const data = {
      key: 'CacheDurationSeconds',
      value: value,
      description: 'Duration in seconds for which redirects are cached in memory.'
    };

    const sub = this.settingsService.setSetting(data).subscribe({
      next: () => {
        this.saving = false;
        this.successMessage = 'Settings saved successfully.';

        setTimeout(() => {
          this.successMessage = '';
          this.cdr.detectChanges();
        }, 3000);

        this.cdr.detectChanges();
      },
      error: (err) => {
        this.saving = false;
        this.errorMessage = err.error?.detail || err.error?.title || 'Failed to save settings.';
        console.error('Error saving settings:', err);
        this.cdr.detectChanges();
      }
    });

    this.subscription.add(sub);
  }
}

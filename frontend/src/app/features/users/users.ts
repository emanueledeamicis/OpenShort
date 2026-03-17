import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/services/auth.service';
import { UserService, AdminUser } from '../../core/services/user.service';

@Component({
    selector: 'app-users',
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
    templateUrl: './users.html'
})
export class UsersComponent implements OnInit, OnDestroy {
    private static readonly loadUsersErrorMessage = 'Unable to load admin users.';
    private static readonly createUserErrorMessage = 'Failed to create admin user. Please try again.';
    private static readonly deleteUserErrorMessage = 'Failed to delete admin user. Please try again.';

    users: AdminUser[] = [];
    loading = false;
    saving = false;
    deleting = false;
    pageErrorMessage = '';
    dialogErrorMessage = '';
    successMessage = '';
    showCreateDialog = false;
    showDeleteConfirm = false;
    selectedUser: AdminUser | null = null;
    currentUserId: string | null = null;
    userForm: FormGroup;
    private subscription = new Subscription();

    constructor(
        private authService: AuthService,
        private userService: UserService,
        private fb: FormBuilder,
        private cdr: ChangeDetectorRef
    ) {
        this.userForm = this.fb.group({
            email: ['', [Validators.required, Validators.email]],
            password: ['', [Validators.required, Validators.minLength(6)]],
            confirmPassword: ['', Validators.required]
        });
    }

    ngOnInit() {
        this.currentUserId = this.authService.getCurrentUserId();
        this.loadUsers();
    }

    ngOnDestroy() {
        this.subscription.unsubscribe();
    }

    loadUsers() {
        this.loading = true;
        this.pageErrorMessage = '';
        this.cdr.detectChanges();

        const sub = this.userService.getAll().subscribe({
            next: (users) => {
                this.users = users.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
                this.loading = false;
                this.cdr.detectChanges();
            },
            error: (err) => {
                this.pageErrorMessage = this.extractApiErrorMessage(err, UsersComponent.loadUsersErrorMessage);
                this.loading = false;
                this.cdr.detectChanges();
            }
        });

        this.subscription.add(sub);
    }

    openCreateDialog() {
        this.userForm.reset();
        this.dialogErrorMessage = '';
        this.successMessage = '';
        this.showCreateDialog = true;
    }

    closeCreateDialog() {
        this.showCreateDialog = false;
        this.dialogErrorMessage = '';
    }

    saveUser() {
        if (this.userForm.invalid) {
            this.userForm.markAllAsTouched();
            return;
        }

        const formValue = this.userForm.value;
        if (formValue.password !== formValue.confirmPassword) {
            this.dialogErrorMessage = 'Passwords do not match.';
            return;
        }

        this.saving = true;
        this.dialogErrorMessage = '';
        this.successMessage = '';
        this.cdr.detectChanges();

        const sub = this.userService.create({
            email: formValue.email,
            password: formValue.password
        }).subscribe({
            next: () => {
                this.saving = false;
                this.showCreateDialog = false;
                this.successMessage = 'Admin user created successfully.';
                this.loadUsers();
            },
            error: (err) => {
                this.saving = false;
                this.dialogErrorMessage = this.extractApiErrorMessage(err, UsersComponent.createUserErrorMessage);
                this.cdr.detectChanges();
            }
        });

        this.subscription.add(sub);
    }

    openDeleteConfirm(user: AdminUser) {
        if (this.isCurrentUser(user)) {
            this.pageErrorMessage = 'You cannot delete the currently signed-in user.';
            return;
        }

        this.selectedUser = user;
        this.pageErrorMessage = '';
        this.successMessage = '';
        this.showDeleteConfirm = true;
    }

    closeDeleteConfirm() {
        this.showDeleteConfirm = false;
        this.selectedUser = null;
    }

    confirmDelete() {
        if (!this.selectedUser) {
            return;
        }

        this.deleting = true;
        this.pageErrorMessage = '';
        this.successMessage = '';
        this.cdr.detectChanges();

        const sub = this.userService.delete(this.selectedUser.id).subscribe({
            next: () => {
                this.deleting = false;
                this.showDeleteConfirm = false;
                this.selectedUser = null;
                this.successMessage = 'Admin user deleted successfully.';
                this.loadUsers();
            },
            error: (err) => {
                this.deleting = false;
                this.pageErrorMessage = this.extractApiErrorMessage(err, UsersComponent.deleteUserErrorMessage);
                this.cdr.detectChanges();
            }
        });

        this.subscription.add(sub);
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

    isCurrentUser(user: AdminUser): boolean {
        return !!this.currentUserId && user.id === this.currentUserId;
    }
}

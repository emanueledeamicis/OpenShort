import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';

export interface AuthResponse {
    token: string;
    email: string | null;
    userName: string;
}

export interface InitialSetupStatus {
    isSetupRequired: boolean;
    userName: string;
}

@Injectable({
    providedIn: 'root'
})
export class AuthService {
    private apiUrl = '/api/auth';
    private isAuthenticatedSubject: BehaviorSubject<boolean>;
    public redirectUrl: string | null = null;

    constructor(private http: HttpClient) {
        // Initialize based on token existence
        const hasToken = !!localStorage.getItem('auth_token');
        this.isAuthenticatedSubject = new BehaviorSubject<boolean>(hasToken);
    }

    login(identifier: string, password: string): Observable<AuthResponse> {
        return this.http.post<AuthResponse>(`${this.apiUrl}/login`, { identifier, password }).pipe(
            tap(response => this.handleAuthSuccess(response))
        );
    }

    getInitialSetupStatus(): Observable<InitialSetupStatus> {
        return this.http.get<InitialSetupStatus>(`${this.apiUrl}/setup-status`);
    }

    setupAdmin(password: string, confirmPassword: string): Observable<AuthResponse> {
        return this.http.post<AuthResponse>(`${this.apiUrl}/setup-admin`, { password, confirmPassword }).pipe(
            tap(response => this.handleAuthSuccess(response))
        );
    }

    logout(): void {
        localStorage.removeItem('auth_token');
        this.isAuthenticatedSubject.next(false);
        // Optional: Call backend to revoke if needed, but JWT is stateless usually
        // this.http.post(`${this.apiUrl}/logout`, {}).subscribe(); 
    }

    isAuthenticated(): boolean {
        return this.isAuthenticatedSubject.value;
    }

    isAuthenticated$(): Observable<boolean> {
        return this.isAuthenticatedSubject.asObservable();
    }

    private handleAuthSuccess(response: AuthResponse): void {
        if (response?.token) {
            localStorage.setItem('auth_token', response.token);
            this.isAuthenticatedSubject.next(true);
        }
    }
}

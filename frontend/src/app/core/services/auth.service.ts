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
    private tokenStorageKey = 'auth_token';
    private isAuthenticatedSubject: BehaviorSubject<boolean>;
    public redirectUrl: string | null = null;

    constructor(private http: HttpClient) {
        const hasToken = !!this.getToken();
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
        localStorage.removeItem(this.tokenStorageKey);
        this.isAuthenticatedSubject.next(false);
    }

    isAuthenticated(): boolean {
        return !!this.getToken();
    }

    isAuthenticated$(): Observable<boolean> {
        return this.isAuthenticatedSubject.asObservable();
    }

    getToken(): string | null {
        const token = localStorage.getItem(this.tokenStorageKey);
        if (!token) {
            return null;
        }

        if (!this.isTokenValid(token)) {
            this.logout();
            return null;
        }

        return token;
    }

    private handleAuthSuccess(response: AuthResponse): void {
        if (response?.token) {
            localStorage.setItem(this.tokenStorageKey, response.token);
            this.isAuthenticatedSubject.next(true);
        }
    }

    private isTokenValid(token: string): boolean {
        try {
            const payloadBase64 = token.split('.')[1];
            if (!payloadBase64) {
                return false;
            }

            const normalizedPayload = payloadBase64.replace(/-/g, '+').replace(/_/g, '/');
            const payloadJson = atob(normalizedPayload.padEnd(Math.ceil(normalizedPayload.length / 4) * 4, '='));
            const payload = JSON.parse(payloadJson) as { exp?: number };

            if (typeof payload.exp !== 'number') {
                return false;
            }

            return payload.exp * 1000 > Date.now();
        } catch {
            return false;
        }
    }
}

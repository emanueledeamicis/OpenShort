import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface ApiKeyInfo {
    exists: boolean;
    prefix: string | null;
    createdAt: string | null;
}

export interface ApiKeyGenerated {
    key: string;
    prefix: string;
    createdAt: string;
}

export interface ChangePasswordRequest {
    currentPassword: string;
    newPassword: string;
    confirmPassword: string;
}

export interface TwoFactorStatus {
    isEnabled: boolean;
    recoveryCodesLeft: number;
}

export interface TwoFactorSetup {
    sharedKey: string;
    authenticatorUri: string;
}

export interface TwoFactorRecoveryCodes {
    recoveryCodes: string[];
}

@Injectable({
    providedIn: 'root'
})
export class SecurityService {
    private apiUrl = '/api/security';

    constructor(private http: HttpClient) { }

    getApiKeyInfo(): Observable<ApiKeyInfo> {
        return this.http.get<ApiKeyInfo>(`${this.apiUrl}/apikey`);
    }

    generateApiKey(): Observable<ApiKeyGenerated> {
        return this.http.post<ApiKeyGenerated>(`${this.apiUrl}/apikey`, {});
    }

    changePassword(request: ChangePasswordRequest): Observable<{ message: string }> {
        return this.http.post<{ message: string }>(`${this.apiUrl}/change-password`, request);
    }

    getTwoFactorStatus(): Observable<TwoFactorStatus> {
        return this.http.get<TwoFactorStatus>(`${this.apiUrl}/two-factor`);
    }

    setupTwoFactor(password: string): Observable<TwoFactorSetup> {
        return this.http.post<TwoFactorSetup>(`${this.apiUrl}/two-factor/setup`, { password });
    }

    enableTwoFactor(code: string): Observable<TwoFactorRecoveryCodes> {
        return this.http.post<TwoFactorRecoveryCodes>(`${this.apiUrl}/two-factor/enable`, { code });
    }

    generateRecoveryCodes(password: string): Observable<TwoFactorRecoveryCodes> {
        return this.http.post<TwoFactorRecoveryCodes>(`${this.apiUrl}/two-factor/recovery-codes`, { password });
    }

    disableTwoFactor(password: string): Observable<{ message: string }> {
        return this.http.post<{ message: string }>(`${this.apiUrl}/two-factor/disable`, { password });
    }
}

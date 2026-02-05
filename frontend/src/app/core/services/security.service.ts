import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface ApiKeyInfo {
    exists: boolean;
    prefix: string | null;
    createdAt: Date | null;
}

export interface ApiKeyGenerated {
    key: string;
    prefix: string;
    createdAt: Date;
}

export interface ChangePasswordRequest {
    currentPassword: string;
    newPassword: string;
    confirmPassword: string;
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
}

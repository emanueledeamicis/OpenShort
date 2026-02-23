import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SystemSetting {
    id: number;
    key: string;
    value: string;
    description: string;
    createdAt: string;
    updatedAt: string | null;
}

export interface SetSettingDto {
    key: string;
    value: string;
    description?: string;
}

export interface SettingResponse {
    key: string;
    value: string;
}

@Injectable({
    providedIn: 'root'
})
export class SettingsService {
    private apiUrl = '/api/settings';

    constructor(private http: HttpClient) { }

    getAllSettings(): Observable<SystemSetting[]> {
        return this.http.get<SystemSetting[]>(this.apiUrl);
    }

    getSetting(key: string): Observable<SettingResponse> {
        return this.http.get<SettingResponse>(`${this.apiUrl}/${key}`);
    }

    setSetting(data: SetSettingDto): Observable<void> {
        return this.http.post<void>(this.apiUrl, data);
    }
}

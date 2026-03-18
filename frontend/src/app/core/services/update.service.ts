import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface UpdateStatusResponse {
    latestVersion: string | null;
}

@Injectable({
    providedIn: 'root'
})
export class UpdateService {
    private apiUrl = '/api/update';

    constructor(private http: HttpClient) { }

    getLatestVersion(): Observable<UpdateStatusResponse> {
        return this.http.get<UpdateStatusResponse>(this.apiUrl);
    }
}

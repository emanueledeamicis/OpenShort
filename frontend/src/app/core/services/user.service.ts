import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AdminUser {
    id: string;
    userName: string;
    email: string | null;
    createdAt: string;
}

export interface CreateAdminUserRequest {
    email: string;
    password: string;
}

@Injectable({
    providedIn: 'root'
})
export class UserService {
    private apiUrl = '/api/users';

    constructor(private http: HttpClient) { }

    getAll(): Observable<AdminUser[]> {
        return this.http.get<AdminUser[]>(this.apiUrl);
    }

    create(request: CreateAdminUserRequest): Observable<AdminUser> {
        return this.http.post<AdminUser>(this.apiUrl, request);
    }

    delete(id: string): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/${id}`);
    }
}

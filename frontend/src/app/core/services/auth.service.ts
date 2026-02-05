import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';

@Injectable({
    providedIn: 'root'
})
export class AuthService {
    private apiUrl = '/api/auth';
    private isAuthenticatedSubject = new BehaviorSubject<boolean>(false);
    public redirectUrl: string | null = null;

    constructor(private http: HttpClient) { }

    login(email: string, password: string): Observable<any> {
        return this.http.post(`${this.apiUrl}/login?useCookies=true`, { email, password }).pipe(
            tap(() => this.isAuthenticatedSubject.next(true))
        );
    }

    logout(): Observable<any> {
        return this.http.post(`${this.apiUrl}/logout`, {}).pipe(
            tap(() => this.isAuthenticatedSubject.next(false))
        );
    }

    isAuthenticated(): boolean {
        return this.isAuthenticatedSubject.value;
    }

    isAuthenticated$(): Observable<boolean> {
        return this.isAuthenticatedSubject.asObservable();
    }
}

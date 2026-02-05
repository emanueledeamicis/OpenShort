import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, tap } from 'rxjs';

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

    login(email: string, password: string): Observable<any> {
        return this.http.post<any>(`${this.apiUrl}/login`, { email, password }).pipe(
            tap(response => {
                if (response && response.token) {
                    localStorage.setItem('auth_token', response.token);
                    this.isAuthenticatedSubject.next(true);
                }
            })
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
}

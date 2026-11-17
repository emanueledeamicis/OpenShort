import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { AuthService } from '../services/auth.service';

const loginUrl = '/api/auth/login';
const loginRoute = '/login';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    // Null when there is no token or when it has expired
    const token = authService.getToken();

    const request = token
        ? req.clone({
            setHeaders: {
                Authorization: `Bearer ${token}`
            }
        })
        : req;

    return next(request).pipe(
        tap({
            error: (error: unknown) => {
                // On the login call a 401 means wrong credentials, not a session that ended
                if (!(error instanceof HttpErrorResponse) || error.status !== 401 || req.url.endsWith(loginUrl)) {
                    return;
                }

                // The session is over: the backend rejected the token, or the token had already expired
                // and the call was sent without it. Several calls can fail together: only the first one
                // signs out, and a token obtained in the meantime is left alone.
                if (authService.getToken() !== token) {
                    return;
                }

                // A 403 is not handled here: the user is known but not allowed, and the page shows the error
                authService.logout();
                if (!router.url.startsWith(loginRoute)) {
                    authService.redirectUrl = router.url;
                    router.navigate([loginRoute]);
                }
            }
        })
    );
};

import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap, throwError, catchError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { environment } from '../../environments/environment';

/**
 * Attaches the Bearer token to every API request.
 * On a 401 response, attempts one token refresh then retries.
 * If refresh fails, the user is signed out.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  const withAuth = (r: HttpRequest<unknown>) => {
    const token = auth.getToken();
    return token
      ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : r;
  };

  // Only intercept requests to our own API
  if (!req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  return next(withAuth(req)).pipe(
    catchError((err) => {
      if (err instanceof HttpErrorResponse && err.status === 401) {
        // Avoid infinite loop on the refresh endpoint itself
        if (req.url.includes('/auth/refresh') || req.url.includes('/auth/google')) {
          return throwError(() => err);
        }

        return from(auth.refreshToken()).pipe(
          switchMap(() => next(withAuth(req))),
          catchError((retryErr) => throwError(() => retryErr))
        );
      }
      return throwError(() => err);
    })
  );
};

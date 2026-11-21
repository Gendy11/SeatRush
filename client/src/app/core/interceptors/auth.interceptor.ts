import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';

const withToken = (req: HttpRequest<unknown>, token: string) =>
  req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

/**
 * Adds the in-memory access token to every API call. If the API answers 401 (token expired or the server
 * restarted with a new key), it gets a fresh token once and retries the request.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.apiUrl)) return next(req);

  const auth = inject(AuthService);

  return auth.getToken().pipe(
    switchMap((token) => next(withToken(req, token))),
    catchError((err: HttpErrorResponse) =>
      err.status === 401
        ? auth.refresh().pipe(switchMap((token) => next(withToken(req, token))))
        : throwError(() => err),
    ),
  );
};

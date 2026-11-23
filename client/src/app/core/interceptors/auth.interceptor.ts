import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';

const withToken = (req: HttpRequest<unknown>, token: string) =>
  req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

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

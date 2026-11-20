import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { UserService } from '../services/user.service';

/** Sends the anonymous user id with every API call (X-User-Id header). */
export const userIdInterceptor: HttpInterceptorFn = (req, next) => {
  const userId = inject(UserService).userId;
  return next(req.clone({ setHeaders: { 'X-User-Id': userId } }));
};

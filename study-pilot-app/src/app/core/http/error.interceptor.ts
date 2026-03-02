import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { Router } from '@angular/router';
import { ToastService } from '../../shared/services/toast.service';
import { ErrorBannerService } from '../services/error-banner.service';
import type { AppError } from '../models/app-error.model';

const SKIP_REFRESH = 'X-Skip-Refresh';

function getFirstMessage(err: HttpErrorResponse): string | undefined {
  const body = err.error;
  if (body && typeof body === 'object' && Array.isArray(body.errors) && body.errors.length > 0) {
    const first = body.errors[0] as AppError;
    return first?.message;
  }
  return undefined;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const toast = inject(ToastService);
  const banner = inject(ErrorBannerService);
  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse && err.status === 401) {
        if (req.headers.has(SKIP_REFRESH)) {
          auth.logout();
          router.navigate(['/auth/login']);
          return throwError(() => err);
        }
        const refreshToken = auth.getRefreshToken();
        if (refreshToken) {
          return auth.refresh().pipe(
            switchMap(success => {
              if (success) return next(req.clone({ setHeaders: { [SKIP_REFRESH]: '1' } }));
              auth.logout();
              router.navigate(['/auth/login']);
              return throwError(() => err);
            }),
            catchError(() => {
              auth.logout();
              router.navigate(['/auth/login']);
              return throwError(() => err);
            })
          );
        }
        auth.logout();
        router.navigate(['/auth/login']);
        return throwError(() => err);
      }
      if (err instanceof HttpErrorResponse && err.status === 429) {
        const msg = getFirstMessage(err) ?? 'Rate limit exceeded. Please try again in a moment.';
        toast.error(msg);
        return throwError(() => err);
      }
      if (err instanceof HttpErrorResponse && err.status === 503) {
        const msg = getFirstMessage(err) ?? 'Service temporarily unavailable.';
        banner.setSystemError(msg, (err.error as { correlationId?: string })?.correlationId);
        toast.error(msg);
        return throwError(() => err);
      }
      if (err instanceof HttpErrorResponse && err.status >= 500) {
        const msg = getFirstMessage(err);
        if (msg) toast.error(msg);
      }
      return throwError(() => err);
    })
  );
};

import { HttpInterceptorFn, HttpResponse, HttpErrorResponse } from '@angular/common/http';
import { map, catchError, throwError } from 'rxjs';
import { ApiResponse } from '@shared/models/api-response.model';
import { EnterpriseApiError } from './enterprise-api-error';
import type { AppError } from '../models/app-error.model';

function isApiResponse(body: unknown): body is ApiResponse<unknown> {
  return typeof body === 'object' && body !== null && 'success' in body;
}

function toEnterpriseError(err: HttpErrorResponse): unknown {
  const body = err.error;
  if (body && typeof body === 'object' && (body as { success?: boolean }).success === false && Array.isArray((body as { errors?: unknown }).errors)) {
    const errors = (body as { errors: AppError[] }).errors;
    const correlationId = (body as { correlationId?: string }).correlationId;
    return new EnterpriseApiError(errors, correlationId);
  }
  return err;
}

export const apiResponseInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    map(event => {
      if (event instanceof HttpResponse && event.body && isApiResponse(event.body)) {
        const body = event.body as ApiResponse<unknown>;
        if (!body.success) {
          const errors = Array.isArray(body.errors) ? body.errors : [];
          throw new EnterpriseApiError(errors, body.correlationId);
        }
        return event.clone({ body: body.data });
      }
      return event;
    }),
    catchError(err => throwError(() => (err instanceof HttpErrorResponse ? toEnterpriseError(err) : err)))
  );
};

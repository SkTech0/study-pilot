import { HttpInterceptorFn, HttpResponse, HttpErrorResponse } from '@angular/common/http';
import { map, catchError, throwError } from 'rxjs';
import { ApiResponse } from '@shared/models/api-response.model';
import { EnterpriseApiError } from './enterprise-api-error';
import type { AppError } from '../models/app-error.model';

function isApiResponse(body: unknown): body is ApiResponse<unknown> {
  if (body === null || typeof body !== 'object') return false;
  const o = body as Record<string, unknown>;
  return 'success' in o || 'Success' in o;
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
        const body = event.body as unknown as Record<string, unknown>;
        const success = body['success'] ?? body['Success'];
        if (success !== true) {
          const errors = Array.isArray(body['errors']) ? (body['errors'] as AppError[]) : [];
          const correlationId = (body['correlationId'] ?? body['CorrelationId']) as string | undefined;
          throw new EnterpriseApiError(errors, correlationId);
        }
        const data = body['data'] ?? body['Data'];
        return event.clone({ body: data });
      }
      return event;
    }),
    catchError(err => throwError(() => (err instanceof HttpErrorResponse ? toEnterpriseError(err) : err)))
  );
};

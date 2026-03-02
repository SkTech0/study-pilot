import { ErrorHandler, Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { ToastService } from '@shared/services/toast.service';
import { ErrorBannerService } from './error-banner.service';
import { EnterpriseApiError } from '../http/enterprise-api-error';

@Injectable()
export class GlobalErrorHandlerService implements ErrorHandler {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly banner = inject(ErrorBannerService);

  handleError(error: unknown): void {
    if (error instanceof EnterpriseApiError) {
      if (error.hasValidationErrors && !error.hasBusinessErrors && !error.hasSystemErrors) {
        return;
      }
      if (error.hasBusinessErrors) {
        const msg = error.errors.find(e => e.severity === 'Business')?.message ?? error.errors[0]?.message;
        if (msg) this.toast.error(msg);
        return;
      }
      if (error.hasSystemErrors) {
        const first = error.errors.find(e => e.severity === 'System');
        const message = first?.message ?? error.errors[0]?.message ?? 'A system error occurred.';
        const correlationId = error.correlationId ?? first?.correlationId;
        this.banner.setSystemError(message, correlationId);
        this.toast.error(correlationId ? `${message} (Ref: ${correlationId})` : message);
        return;
      }
      const msg = error.errors[0]?.message;
      if (msg) this.toast.error(msg);
      return;
    }
    const err = error as { status?: number };
    if (err?.status === 401) {
      this.auth.logout();
      this.router.navigate(['/auth/login']);
      return;
    }
    this.banner.setSystemError('An unexpected error occurred. Please try again.');
  }
}

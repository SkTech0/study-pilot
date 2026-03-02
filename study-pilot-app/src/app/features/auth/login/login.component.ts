import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@shared/services/toast.service';
import { LoadingService } from '@core/services/loading.service';
import { EnterpriseApiError } from '@core/http/enterprise-api-error';
import { mapErrorsToFormControls } from '@core/utils/form-error-mapper';

@Component({
  selector: 'app-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gray-100 p-4">
      <div class="w-full max-w-md card">
        <div class="text-center mb-6">
          <p class="text-sm font-medium text-blue-600 tracking-wide">StudyPilot</p>
          <h1 class="text-2xl font-semibold text-gray-900 mt-2">Welcome back</h1>
          <p class="mt-1 text-sm text-gray-500">Sign in to pick up where you left off</p>
        </div>
        <form [formGroup]="form" (ngSubmit)="onSubmit()">
          <div class="mb-4">
            <label for="login-email" class="label-base">Email</label>
            <input id="login-email" type="email" formControlName="email" class="input-base"
                   placeholder="you@example.com" autocomplete="email" />
            @if (form.get('email')?.invalid && form.get('email')?.touched) {
              <p class="text-red-600 text-xs mt-1" role="alert">{{ form.get('email')?.getError('serverErrors')?.[0] ?? form.get('email')?.getError('serverMessage') ?? 'Valid email required' }}</p>
            }
          </div>
          <div class="mb-5">
            <div class="flex items-center justify-between mb-1.5">
              <label for="login-password" class="label-base mb-0">Password</label>
              <button type="button" (click)="showPassword.set(!showPassword())" class="text-xs font-medium text-gray-500 hover:text-gray-700"
                      [attr.aria-pressed]="showPassword()">{{ showPassword() ? 'Hide' : 'Show' }}</button>
            </div>
            <input id="login-password" [type]="showPassword() ? 'text' : 'password'" formControlName="password"
                   class="input-base" autocomplete="current-password" />
            @if (form.get('password')?.invalid && form.get('password')?.touched) {
              <p class="text-red-600 text-xs mt-1" role="alert">{{ form.get('password')?.getError('serverErrors')?.[0] ?? form.get('password')?.getError('serverMessage') ?? 'Password required' }}</p>
            }
          </div>
          <button type="submit" [disabled]="form.invalid || loading()" class="btn-primary w-full">
            @if (loading()) { Signing in… } @else { Sign in }
          </button>
        </form>
        <p class="mt-5 text-center text-sm text-gray-600">
          No account? <a routerLink="/auth/register" class="font-medium text-blue-600 hover:text-blue-700 hover:underline">Register</a>
        </p>
      </div>
    </div>
  `
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  loading = inject(LoadingService).isLoading;
  showPassword = signal(false);

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required]
  });

  onSubmit(): void {
    if (this.form.invalid) return;
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: err => {
        if (err instanceof EnterpriseApiError && err.hasValidationErrors) {
          mapErrorsToFormControls(this.form, err.errors);
        } else if (err instanceof EnterpriseApiError && err.errors.length > 0) {
          this.toast.error(err.errors.map(e => e.message).join(' '));
        } else {
          this.toast.error((err as Error)?.message ?? 'Request failed.');
        }
      }
    });
  }
}

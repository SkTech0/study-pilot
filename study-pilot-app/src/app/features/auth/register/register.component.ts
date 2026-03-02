import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '@core/auth/auth.service';
import { ToastService } from '@shared/services/toast.service';
import { LoadingService } from '@core/services/loading.service';
import { EnterpriseApiError } from '@core/http/enterprise-api-error';
import { mapErrorsToFormControls } from '@core/utils/form-error-mapper';

@Component({
  selector: 'app-register',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gray-100 p-4">
      <div class="w-full max-w-sm bg-white rounded-lg shadow p-6">
        <h1 class="text-xl font-semibold mb-4">Create account</h1>
        <form [formGroup]="form" (ngSubmit)="onSubmit()">
          <div class="mb-4">
            <label class="block text-sm font-medium text-gray-700 mb-1">Email</label>
            <input type="email" formControlName="email" class="w-full border rounded px-3 py-2"
                   placeholder="you@example.com" />
            @if (form.get('email')?.invalid && form.get('email')?.touched) {
              <p class="text-red-600 text-xs mt-1">{{ form.get('email')?.getError('serverErrors')?.[0] ?? form.get('email')?.getError('serverMessage') ?? 'Valid email required' }}</p>
            }
          </div>
          <div class="mb-4">
            <label class="block text-sm font-medium text-gray-700 mb-1">Password</label>
            <input type="password" formControlName="password" class="w-full border rounded px-3 py-2" />
            @if (form.get('password')?.invalid && form.get('password')?.touched) {
              <p class="text-red-600 text-xs mt-1">{{ form.get('password')?.getError('serverErrors')?.[0] ?? form.get('password')?.getError('serverMessage') ?? 'Password required (min 6 characters)' }}</p>
            }
          </div>
          <button type="submit" [disabled]="form.invalid || loading()"
                  class="w-full bg-blue-600 text-white py-2 rounded hover:bg-blue-700 disabled:opacity-50">
            Register
          </button>
        </form>
        <p class="mt-4 text-sm text-gray-600">
          Already have an account? <a routerLink="/auth/login" class="text-blue-600 hover:underline">Sign in</a>
        </p>
      </div>
    </div>
  `
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  loading = inject(LoadingService).isLoading;

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  onSubmit(): void {
    if (this.form.invalid) return;
    const { email, password } = this.form.getRawValue();
    this.auth.register(email, password).subscribe({
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

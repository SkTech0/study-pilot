import { AbstractControl, FormGroup } from '@angular/forms';
import type { AppError } from '../models/app-error.model';

export function mapErrorsToFormControls(
  form: FormGroup,
  errors: AppError[],
  fieldMapping?: Record<string, string>
): void {
  const byField = new Map<string, AppError[]>();
  for (const err of errors) {
    const field = fieldMapping?.[err.field ?? ''] ?? err.field ?? '';
    if (!field) continue;
    const list = byField.get(field) ?? [];
    list.push(err);
    byField.set(field, list);
  }
  for (const [fieldName, errs] of byField) {
    const control = form.get(fieldName);
    if (control) {
      const serverErrors = errs.map(e => e.message);
      control.setErrors({ ...control.errors, server: true, serverErrors });
      control.markAsTouched();
    }
  }
}

export function setControlError(control: AbstractControl | null, error: AppError): void {
  if (!control) return;
  control.setErrors({ ...control.errors, server: true, serverMessage: error.message });
  control.markAsTouched();
}

export function setControlErrors(control: AbstractControl | null, errors: AppError[]): void {
  if (!control || errors.length === 0) return;
  const messages = errors.map(e => e.message);
  control.setErrors({ ...control.errors, server: true, serverErrors: messages });
  control.markAsTouched();
}

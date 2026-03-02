import type { AppError } from '../models/app-error.model';

export class EnterpriseApiError extends Error {
  constructor(
    public readonly errors: AppError[],
    public readonly correlationId?: string
  ) {
    const first = errors[0];
    super(first?.message ?? 'Request failed');
    this.name = 'EnterpriseApiError';
    Object.setPrototypeOf(this, EnterpriseApiError.prototype);
  }

  get hasValidationErrors(): boolean {
    return this.errors.some(e => e.severity === 'Validation');
  }

  get hasBusinessErrors(): boolean {
    return this.errors.some(e => e.severity === 'Business');
  }

  get hasSystemErrors(): boolean {
    return this.errors.some(e => e.severity === 'System');
  }

  getErrorsByField(): Map<string, AppError[]> {
    const map = new Map<string, AppError[]>();
    for (const err of this.errors) {
      const key = err.field ?? '';
      const list = map.get(key) ?? [];
      list.push(err);
      map.set(key, list);
    }
    return map;
  }
}

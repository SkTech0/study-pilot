export type ErrorSeverity = 'Validation' | 'Business' | 'System';

export interface AppError {
  code: string;
  message: string;
  field?: string;
  severity: ErrorSeverity;
  correlationId?: string;
}

export interface EnterpriseErrorResponse {
  success: false;
  errors: AppError[];
  correlationId?: string;
}

export function isEnterpriseErrorResponse(body: unknown): body is EnterpriseErrorResponse {
  return (
    typeof body === 'object' &&
    body !== null &&
    'success' in body &&
    (body as { success: unknown }).success === false &&
    'errors' in body &&
    Array.isArray((body as { errors: unknown }).errors)
  );
}

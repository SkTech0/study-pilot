import type { AppError } from '../../core/models/app-error.model';

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  errors?: AppError[];
  correlationId?: string;
}

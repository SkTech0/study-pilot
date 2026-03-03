import { Injectable, signal, computed } from '@angular/core';

export interface SystemErrorDisplay {
  message: string;
  correlationId?: string;
  /** When set, UI can show a retry action that clears the banner and calls this. */
  onRetry?: () => void;
}

@Injectable({ providedIn: 'root' })
export class ErrorBannerService {
  private readonly _current = signal<SystemErrorDisplay | null>(null);
  readonly currentSystemError = this._current.asReadonly();
  readonly hasSystemError = computed(() => this._current() !== null);

  setSystemError(message: string, correlationId?: string, onRetry?: () => void): void {
    this._current.set({ message, correlationId, onRetry });
  }

  clear(): void {
    this._current.set(null);
  }

  /** Clear and run retry callback if present. Call from retry button. */
  retry(): void {
    const current = this._current();
    this._current.set(null);
    current?.onRetry?.();
  }
}

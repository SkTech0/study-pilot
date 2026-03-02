import { Injectable, signal, computed } from '@angular/core';

export interface SystemErrorDisplay {
  message: string;
  correlationId?: string;
}

@Injectable({ providedIn: 'root' })
export class ErrorBannerService {
  private readonly _current = signal<SystemErrorDisplay | null>(null);
  readonly currentSystemError = this._current.asReadonly();
  readonly hasSystemError = computed(() => this._current() !== null);

  setSystemError(message: string, correlationId?: string): void {
    this._current.set({ message, correlationId });
  }

  clear(): void {
    this._current.set(null);
  }
}

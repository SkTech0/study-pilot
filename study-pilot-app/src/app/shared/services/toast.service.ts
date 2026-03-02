import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  id: number;
  message: string;
  type: 'error' | 'success' | 'info';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 0;
  private readonly _toasts = signal<ToastMessage[]>([]);
  readonly toasts = this._toasts.asReadonly();

  error(message: string): void {
    this.add(message, 'error');
  }

  success(message: string): void {
    this.add(message, 'success');
  }

  private add(message: string, type: ToastMessage['type']): void {
    const id = ++this.nextId;
    this._toasts.update(t => [...t, { id, message, type }]);
    setTimeout(() => this.remove(id), 5000);
  }

  remove(id: number): void {
    this._toasts.update(t => t.filter(x => x.id !== id));
  }
}

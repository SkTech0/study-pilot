import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { ToastService } from '@shared/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fixed bottom-4 right-4 z-50 flex flex-col gap-2">
      @for (t of toast.toasts(); track t.id) {
        <div
          role="alert"
          class="px-4 py-3 rounded shadow-lg text-sm"
          [class]="t.type === 'error' ? 'bg-red-600 text-white' : t.type === 'success' ? 'bg-green-600 text-white' : 'bg-gray-700 text-white'"
        >
          <span>{{ t.message }}</span>
          <button type="button" class="ml-2 underline" (click)="toast.remove(t.id)">Dismiss</button>
        </div>
      }
    </div>
  `
})
export class ToastComponent {
  readonly toast = inject(ToastService);
}

import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { ToastService } from '@shared/services/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fixed bottom-4 right-4 z-50 flex flex-col gap-2 max-w-sm w-full sm:max-w-md" aria-live="polite">
      @for (t of toast.toasts(); track t.id) {
        <div
          role="alert"
          class="flex items-start justify-between gap-3 px-4 py-3 rounded-lg shadow-md text-sm"
          [class]="t.type === 'error' ? 'bg-red-600 text-white' : t.type === 'success' ? 'bg-green-600 text-white' : 'bg-gray-800 text-white'"
        >
          <span class="flex-1 min-w-0">{{ t.message }}</span>
          <button type="button" class="shrink-0 font-medium underline hover:no-underline focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-transparent rounded" (click)="toast.remove(t.id)">Dismiss</button>
        </div>
      }
    </div>
  `
})
export class ToastComponent {
  readonly toast = inject(ToastService);
}

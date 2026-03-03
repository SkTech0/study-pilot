import { Component, input, ChangeDetectionStrategy } from '@angular/core';

export interface TutorMessageItem {
  role: 'user' | 'assistant';
  content: string;
}

@Component({
  selector: 'app-tutor-message-stream',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-3 min-h-[200px]">
      @if (messages().length === 0 && !loading()) {
        <p class="text-sm text-gray-500">Send a message to start. The tutor will diagnose and teach step by step.</p>
      }
      @for (m of messages(); track $index) {
        <div class="flex" [class.justify-end]="m.role === 'user'" [class.justify-start]="m.role === 'assistant'">
          <div
            class="max-w-[85%] rounded-lg px-3 py-2 text-sm"
            [class.bg-blue-600]="m.role === 'user'"
            [class.text-white]="m.role === 'user'"
            [class.bg-gray-100]="m.role === 'assistant'"
            [class.text-gray-900]="m.role === 'assistant'"
          >
            {{ m.content }}
          </div>
        </div>
      }
      @if (loading()) {
        <div class="flex justify-start">
          <div class="bg-gray-100 rounded-lg px-3 py-2 text-sm text-gray-500">Thinking…</div>
        </div>
      }
    </div>
  `,
})
export class TutorMessageStreamComponent {
  messages = input<TutorMessageItem[]>([]);
  loading = input<boolean>(false);
}

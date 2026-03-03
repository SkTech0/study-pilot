import { Component, input, output, signal, ChangeDetectionStrategy } from '@angular/core';

export interface TutorExerciseItem {
  question: string;
  expectedAnswer: string;
  difficulty: string;
}

@Component({
  selector: 'app-exercise-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (exercise(); as ex) {
      <div class="rounded-lg border border-amber-200 bg-amber-50/50 p-4">
        <h3 class="text-sm font-medium text-amber-900 mb-2">Quick exercise</h3>
        <p class="text-sm text-gray-800 mb-3">{{ ex.question }}</p>
        <textarea
          class="w-full rounded border border-gray-300 px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
          rows="3"
          placeholder="Your answer…"
          [value]="answer()"
          (input)="answer.set($any($event.target).value)"
          [disabled]="submitting()"
        ></textarea>
        <div class="mt-2 flex gap-2">
          <button
            type="button"
            class="btn-primary text-sm"
            [disabled]="!answer().trim() || submitting()"
            (click)="submit.emit(answer().trim())"
          >
            {{ submitting() ? 'Checking…' : 'Submit answer' }}
          </button>
        </div>
        @if (feedback(); as fb) {
          <div class="mt-3 p-2 rounded text-sm" [class.bg-green-100]="fb.isCorrect" [class.text-green-800]="fb.isCorrect" [class.bg-red-100]="!fb.isCorrect" [class.text-red-800]="!fb.isCorrect">
            {{ fb.explanation }}
          </div>
        }
      </div>
    }
  `,
})
export class ExercisePanelComponent {
  exercise = input<TutorExerciseItem | null>(null);
  submitting = input<boolean>(false);
  feedback = input<{ isCorrect: boolean; explanation: string } | null>(null);
  submit = output<string>();

  answer = signal('');
}

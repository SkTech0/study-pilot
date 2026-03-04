import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { StudyPilotApiService, StartTutorResponse, TutorRespondResponse, EvaluateExerciseResponse } from '@core/services/study-pilot-api.service';
import { GoalProgressSidebarComponent, TutorGoalItem } from '../goal-progress-sidebar/goal-progress-sidebar.component';
import { TutorMessageStreamComponent, TutorMessageItem } from '../tutor-message-stream/tutor-message-stream.component';
import { ExercisePanelComponent, TutorExerciseItem } from '../exercise-panel/exercise-panel.component';

@Component({
  selector: 'app-tutor-session-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, FormsModule, GoalProgressSidebarComponent, TutorMessageStreamComponent, ExercisePanelComponent],
  template: `
    <div class="p-4 sm:p-6 max-w-5xl mx-auto">
      <div class="mb-6 flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-semibold text-gray-900">Tutor session</h1>
          <p class="mt-1 text-sm text-gray-500">Guided learning — the tutor will diagnose and teach step by step.</p>
        </div>
        <a routerLink="/dashboard" class="text-sm font-medium text-blue-600 hover:text-blue-700">← Dashboard</a>
      </div>

      @if (!sessionId()) {
        <div class="card">
          <p class="text-gray-600 mb-4">Start a tutor session to get personalized goals from your weak topics. The tutor will guide you through concepts.</p>
          <button type="button" class="btn-primary" [disabled]="starting()" (click)="startSession()">
            {{ starting() ? 'Starting…' : 'Start tutor session' }}
          </button>
          @if (startError()) {
            <p class="mt-2 text-sm text-red-600">{{ startError() }}</p>
          }
        </div>
      } @else {
        <div class="grid gap-4 lg:grid-cols-[1fr_200px]">
          <div class="space-y-4">
            <app-tutor-message-stream [messages]="messages()" [loading]="responding()" />
            <app-exercise-panel
              [exercise]="currentExercise()"
              [submitting]="evaluating()"
              [feedback]="exerciseFeedback()"
              (submit)="onExerciseSubmit($event)"
            />
            @if (sessionId()) {
              <form class="flex gap-2" (ngSubmit)="sendMessage()">
                <input
                  type="text"
                  name="message"
                  class="flex-1 rounded border border-gray-300 px-3 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                  placeholder="Type your message and press Enter or click Send…"
                  [(ngModel)]="inputMessageValue"
                  [disabled]="responding()"
                  aria-label="Your message to the tutor"
                />
                <button type="submit" class="btn-primary" [disabled]="!inputMessageValue.trim() || responding()">
                  Send
                </button>
              </form>
            }
          </div>
          <div>
            <app-goal-progress-sidebar [goals]="goals()" />
          </div>
        </div>
      }
    </div>
  `,
})
export class TutorSessionPageComponent {
  private readonly api = inject(StudyPilotApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  sessionId = signal<string | null>(null);
  goals = signal<TutorGoalItem[]>([]);
  messages = signal<TutorMessageItem[]>([]);
  currentExercise = signal<TutorExerciseItem | null>(null);
  exerciseFeedback = signal<{ isCorrect: boolean; explanation: string } | null>(null);
  currentExerciseId = signal<string | null>(null);

  starting = signal(false);
  startError = signal<string | null>(null);
  responding = signal(false);
  evaluating = signal(false);
  /** Bound to the message input via ngModel so user can type and send reliably. */
  inputMessageValue = '';

  startSession(): void {
    this.starting.set(true);
    this.startError.set(null);
    const documentId = this.route.snapshot.queryParamMap.get('documentId');
    this.api.startTutorSession(documentId || undefined).subscribe({
      next: (res: StartTutorResponse) => {
        this.sessionId.set(res.sessionId);
        this.goals.set(res.goals.map((g) => ({ goalId: g.goalId, conceptId: g.conceptId, conceptName: g.conceptName, goalType: g.goalType, priority: g.priority })));
        this.starting.set(false);
      },
      error: (err) => {
        this.startError.set(err?.error?.message || err?.message || 'Failed to start session');
        this.starting.set(false);
      },
    });
  }

  sendMessage(): void {
    const sid = this.sessionId();
    const msg = this.inputMessageValue.trim();
    if (!sid || !msg) return;
    this.responding.set(true);
    this.messages.update((list) => [...list, { role: 'user', content: msg }]);
    this.inputMessageValue = '';
    this.api.tutorRespond(sid, msg).subscribe({
      next: (res: TutorRespondResponse) => {
        this.messages.update((list) => [...list, { role: 'assistant', content: res.assistantMessage }]);
        if (res.optionalExercise) {
          this.currentExercise.set({
            question: res.optionalExercise.question,
            expectedAnswer: res.optionalExercise.expectedAnswer,
            difficulty: res.optionalExercise.difficulty,
          });
          this.currentExerciseId.set(res.optionalExercise.exerciseId);
        }
        this.responding.set(false);
      },
      error: (err) => {
        this.messages.update((list) => [...list, { role: 'assistant', content: 'Sorry, something went wrong. Please try again.' }]);
        this.responding.set(false);
      },
    });
  }

  onExerciseSubmit(answer: string): void {
    const exerciseId = this.currentExerciseId();
    if (!exerciseId) return;
    this.evaluating.set(true);
    this.exerciseFeedback.set(null);
    this.api.evaluateTutorExercise(exerciseId, answer).subscribe({
      next: (res: EvaluateExerciseResponse) => {
        this.exerciseFeedback.set({ isCorrect: res.isCorrect, explanation: res.explanation });
        this.evaluating.set(false);
      },
      error: () => {
        this.exerciseFeedback.set({ isCorrect: false, explanation: 'Evaluation failed. Please try again.' });
        this.evaluating.set(false);
      },
    });
  }
}

import { Component, inject, ChangeDetectionStrategy, signal, computed, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { StudyPilotApiService, QuizSession, QuizQuestion } from '@core/services/study-pilot-api.service';
import { QuizStateService } from '../quiz-state.service';

@Component({
  selector: 'app-quiz-player',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe],
  template: `
    <div class="p-4 sm:p-6 max-w-2xl mx-auto">
      @if (result()) {
        <div class="card text-center">
          <h2 class="text-xl font-semibold text-gray-900 mb-2">Quiz complete</h2>
          <p class="text-gray-600">Score: {{ result()!.correctCount }} / {{ result()!.totalCount }} ({{ result()!.totalCount ? (result()!.correctCount / result()!.totalCount * 100) : 0 | number:'1.0-0' }}%)</p>
          <button (click)="goDashboard()" class="btn-primary mt-6">Back to dashboard</button>
        </div>
      } @else if (session()) {
        <div class="mb-4 flex items-center justify-between">
          <span class="text-sm font-medium text-gray-500">Question {{ currentIndex() + 1 }} of {{ session()!.questions.length }}</span>
          <div class="h-2 flex-1 max-w-[200px] ml-4 bg-gray-200 rounded-full overflow-hidden">
            <div class="h-full bg-blue-600 rounded-full transition-all" [style.width.%]="(currentIndex() + 1) / session()!.questions.length * 100"></div>
          </div>
        </div>
        @if (currentQuestion(); as q) {
          <div class="card">
            <p class="font-medium text-gray-900 mb-4 text-lg">{{ q.text }}</p>
            <ul class="space-y-2" role="listbox" aria-label="Answer options">
              @for (opt of q.options; track $index) {
                <li>
                  <button type="button"
                          (click)="select($index)"
                          class="w-full text-left px-4 py-3 rounded-lg border-2 transition-colors font-medium"
                          [class.border-blue-500]="selectedIndex() === $index"
                          [class.bg-blue-50]="selectedIndex() === $index"
                          [class.border-gray-200]="selectedIndex() !== $index"
                          [class.hover:border-gray-300]="selectedIndex() !== $index">
                    {{ opt }}
                  </button>
                </li>
              }
            </ul>
            <div class="mt-6 flex gap-2 flex-wrap">
              @if (currentIndex() > 0) {
                <button type="button" (click)="prev()" class="btn-secondary">Previous</button>
              }
              @if (currentIndex() < session()!.questions.length - 1) {
                <button type="button" (click)="next()" [disabled]="selectedIndex() === null" class="btn-primary">Next</button>
              } @else {
                <button type="button" (click)="submit()" [disabled]="selectedIndex() === null" class="btn-primary bg-green-600 hover:bg-green-700">Submit</button>
              }
            </div>
          </div>
        }
      } @else {
        <div class="card text-center py-12">
          <div class="animate-pulse flex flex-col items-center gap-3">
            <span class="block h-4 bg-gray-200 rounded w-48"></span>
            <span class="block h-4 bg-gray-100 rounded w-32"></span>
          </div>
          <p class="mt-4 text-gray-500">Loading quiz…</p>
        </div>
      }
    </div>
  `
})
export class QuizPlayerComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(StudyPilotApiService);
  private readonly state = inject(QuizStateService);
  session = this.state.currentSession;
  result = this.state.currentResult;
  currentIndex = signal(0);
  selectedIndex = signal<number | null>(null);
  answers = signal<Record<string, number>>({});

  currentQuestion = computed(() => {
    const s = this.session();
    const i = this.currentIndex();
    return (s && s.questions[i]) ?? null;
  });

  ngOnInit(): void {
    if (this.session()) return;
    const documentId = this.route.snapshot.paramMap.get('id');
    if (!documentId) {
      this.router.navigate(['/dashboard']);
      return;
    }
    this.api.startQuiz(documentId).subscribe({
      next: s => {
        this.state.setSession(s);
      },
      error: () => this.router.navigate(['/dashboard'])
    });
  }

  select(index: number): void {
    this.selectedIndex.set(index);
    const q = this.currentQuestion();
    if (q) this.answers.update(a => ({ ...a, [q.id]: index }));
  }

  next(): void {
    this.currentIndex.update(i => i + 1);
    const s = this.session();
    const q = s?.questions[this.currentIndex()];
    this.selectedIndex.set(q ? this.answers()[q.id] ?? null : null);
  }

  prev(): void {
    this.currentIndex.update(i => Math.max(0, i - 1));
    const q = this.currentQuestion();
    this.selectedIndex.set(q ? this.answers()[q.id] ?? null : null);
  }

  submit(): void {
    const s = this.session();
    if (!s) return;
    const answers = Object.entries(this.answers()).map(([questionId, idx]) => {
      const q = s.questions.find((x: QuizQuestion) => x.id === questionId);
      return { questionId, submittedAnswer: q ? q.options[idx] ?? '' : '' };
    });
    this.api.submitQuiz({ quizId: s.quizId, answers }).subscribe({
      next: r => {
        this.state.setResult(r);
      },
      error: () => this.router.navigate(['/dashboard'])
    });
  }

  goDashboard(): void {
    this.state.clear();
    this.router.navigate(['/dashboard']);
  }
}

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
    <div class="p-4 max-w-2xl mx-auto">
      @if (result()) {
        <div class="rounded border p-4 bg-gray-50">
          <h2 class="text-lg font-semibold mb-2">Quiz complete</h2>
          <p>Score: {{ result()!.correctCount }} / {{ result()!.totalCount }} ({{ result()!.totalCount ? (result()!.correctCount / result()!.totalCount * 100) : 0 | number:'1.0-0' }}%)</p>
          <button (click)="goDashboard()" class="mt-4 bg-blue-600 text-white px-4 py-2 rounded">Back to dashboard</button>
        </div>
      } @else if (session()) {
        <div class="mb-4">
          <span class="text-gray-600">Question {{ currentIndex() + 1 }} of {{ session()!.questions.length }}</span>
        </div>
        @if (currentQuestion(); as q) {
          <div class="rounded border p-4 bg-white shadow">
            <p class="font-medium mb-4">{{ q.text }}</p>
            <ul class="space-y-2">
              @for (opt of q.options; track $index) {
                <li>
                  <button type="button"
                          (click)="select($index)"
                          class="w-full text-left px-4 py-2 rounded border hover:bg-gray-100"
                          [class.bg-blue-100]="selectedIndex() === $index">
                    {{ opt }}
                  </button>
                </li>
              }
            </ul>
            <div class="mt-4 flex gap-2">
              @if (currentIndex() > 0) {
                <button (click)="prev()" class="px-4 py-2 border rounded">Previous</button>
              }
              @if (currentIndex() < session()!.questions.length - 1) {
                <button (click)="next()" [disabled]="selectedIndex() === null" class="px-4 py-2 bg-blue-600 text-white rounded disabled:opacity-50">Next</button>
              } @else {
                <button (click)="submit()" [disabled]="selectedIndex() === null" class="px-4 py-2 bg-green-600 text-white rounded disabled:opacity-50">Submit</button>
              }
            </div>
          </div>
        }
      } @else {
        <p class="text-gray-600">Loading quiz...</p>
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

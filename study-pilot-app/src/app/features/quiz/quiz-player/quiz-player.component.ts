import { Component, inject, ChangeDetectionStrategy, signal, computed, OnInit, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { StudyPilotApiService, QuizSession, QuizQuestion, QuizResult, GetQuizQuestionResponse } from '@core/services/study-pilot-api.service';
import { QuizStateService } from '../quiz-state.service';
import { QuizPollingService } from '../quiz-polling.service';
import { EnterpriseApiError } from '@core/http/enterprise-api-error';

@Component({
  selector: 'app-quiz-player',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe],
  template: `
    <div class="p-4 sm:p-6 max-w-2xl mx-auto">
      @if (result(); as r) {
        <div class="card text-center">
          <h2 class="text-xl font-semibold text-gray-900 mb-2">Quiz complete</h2>
          <p class="text-gray-600">Score: {{ r.correctCount }} / {{ r.totalCount }} ({{ r.totalCount ? (r.correctCount / r.totalCount * 100) : 0 | number:'1.0-0' }}%)</p>
          @if (r.totalCount && r.correctCount / r.totalCount >= 0.8) {
            <p class="text-green-600 text-sm font-medium mt-1">Nice work!</p>
          } @else if (r.totalCount && r.correctCount / r.totalCount >= 0.5) {
            <p class="text-gray-600 text-sm mt-1">Review your weak topics to improve next time.</p>
          }
          @if (r.questionResults?.length && session(); as s) {
            <div class="mt-6 text-left border-t pt-4">
              <h3 class="text-sm font-semibold text-gray-700 mb-2">Answers</h3>
              <ul class="space-y-2">
                @for (qr of r.questionResults; track qr.questionId) {
                  <li class="text-sm">
                    <span class="font-medium text-gray-900">{{ getQuestionText(qr.questionId) }}</span>
                    <span [class.text-green-600]="qr.isCorrect" [class.text-red-600]="!qr.isCorrect">
                      {{ qr.isCorrect ? ' ✓' : ' ✗' }}
                    </span>
                    @if (!qr.isCorrect && (qr.correctAnswer || qr.correctOptionIndex >= 0)) {
                      <span class="block text-gray-600 mt-0.5">
                        Correct: {{ qr.correctAnswer }}
                        @if (qr.correctOptionIndex >= 0) {
                          <span class="text-gray-500"> (option {{ qr.correctOptionIndex + 1 }})</span>
                        }
                      </span>
                    }
                  </li>
                }
              </ul>
            </div>
          }
          <button (click)="goDashboard()" class="btn-primary mt-6">Back to dashboard</button>
        </div>
      } @else if (session() && (session()!.totalQuestionCount || 0) === 0) {
        <div class="card text-center py-12">
          <h2 class="text-lg font-semibold text-gray-900 mb-2">No questions available</h2>
          <p class="text-gray-600 mb-6">This document has no concepts to quiz. Process the document first.</p>
          <button type="button" (click)="goDashboard()" class="btn-primary">Back to dashboard</button>
        </div>
      } @else if (session()) {
        <div class="mb-4 flex items-center justify-between">
          <span class="text-sm font-medium text-gray-500">Question {{ currentIndex() + 1 }} of {{ session()!.totalQuestionCount }}</span>
          <div class="h-2 flex-1 max-w-[200px] ml-4 bg-gray-200 rounded-full overflow-hidden">
            <div class="h-full bg-blue-600 rounded-full transition-all" [style.width.%]="questionProgress()"></div>
          </div>
        </div>
        @if (isLoading(currentIndex())) {
          <div class="card py-12 text-center">
            <div class="animate-pulse flex flex-col items-center gap-3">
              <span class="block h-4 bg-gray-200 rounded w-3/4 max-w-md"></span>
              <span class="block h-4 bg-gray-100 rounded w-1/2"></span>
              <span class="block h-4 bg-gray-100 rounded w-2/3"></span>
            </div>
            <p class="mt-4 text-gray-500">{{ isGenerating(currentIndex()) ? 'Generating question…' : (isRetrying(currentIndex()) ? 'Retrying…' : 'Preparing your question…') }}</p>
          </div>
        } @else if (isFailed(currentIndex())) {
          <div class="card py-8 text-center">
            <h3 class="text-lg font-semibold text-gray-900 mb-2">This question couldn't be generated</h3>
            <p class="text-gray-600 mb-4">{{ getFailedMessage(currentIndex()) }}</p>
            <p class="text-sm text-gray-500 mb-4">You can retry or continue to the next question.</p>
            <button type="button" (click)="retryQuestion(currentIndex())" class="btn-primary">Retry</button>
          </div>
        } @else if (currentQuestion()) {
          <div class="card">
            <p class="font-medium text-gray-900 mb-4 text-lg">{{ currentQuestion()!.text }}</p>
            <ul class="space-y-2" role="listbox" aria-label="Answer options">
              @for (opt of currentQuestion()!.options; track $index) {
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
              @if (currentIndex() < session()!.totalQuestionCount - 1) {
                <button type="button" (click)="next()" [disabled]="selectedIndex() === null" class="btn-primary">Next</button>
              } @else {
                <button type="button" (click)="submit()" [disabled]="selectedIndex() === null" class="btn-primary bg-green-600 hover:bg-green-700">Submit</button>
              }
            </div>
          </div>
        } @else {
          <div class="card py-8 text-center">
            <p class="text-gray-500 mb-4">Preparing your question…</p>
          </div>
        }
      } @else if (startError()) {
        <div class="card text-center py-12">
          <h2 class="text-lg font-semibold text-gray-900 mb-2">Couldn't start quiz</h2>
          <p class="text-gray-600 mb-2">{{ startError() }}</p>
          <p class="text-sm text-gray-500 mb-6">Make sure the document has finished processing, then try again.</p>
          <button type="button" (click)="goDashboard()" class="btn-primary">Back to dashboard</button>
        </div>
      } @else {
        <div class="card text-center py-12">
          <div class="animate-pulse flex flex-col items-center gap-3">
            <span class="block h-4 bg-gray-200 rounded w-48"></span>
            <span class="block h-4 bg-gray-100 rounded w-32"></span>
          </div>
          <p class="mt-4 text-gray-500">Setting up your quiz…</p>
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
  private readonly polling = inject(QuizPollingService);
  private readonly destroyRef = inject(DestroyRef);
  session = this.state.currentSession;
  result = this.state.currentResult;
  startError = signal<string | null>(null);
  currentIndex = signal(0);
  selectedIndex = signal<number | null>(null);
  answers = signal<Record<string, number>>({});
  private readonly inFlight = new Set<number>();
  private readonly generatingIndexes = new Set<number>();

  currentQuestion = computed(() => {
    const s = this.session();
    const i = this.currentIndex();
    if (!s || i < 0 || i >= s.questions.length) return null;
    const slot = s.questions[i];
    return slot && typeof (slot as QuizQuestion).text === 'string' ? (slot as QuizQuestion) : null;
  });

  questionProgress = computed(() => {
    const s = this.session();
    const total = s?.totalQuestionCount ?? 0;
    if (total === 0) return 0;
    return ((this.currentIndex() + 1) / total) * 100;
  });

  isLoading(index: number): boolean {
    return this.state.loadingIndexesSet().has(index);
  }

  isFailed(index: number): boolean {
    return this.state.failedIndexesSet().has(index);
  }

  isRetrying(index: number): boolean {
    return this.state.loadingIndexesSet().has(index);
  }

  isGenerating(index: number): boolean {
    return this.generatingIndexes.has(index);
  }

  getFailedMessage(index: number): string {
    return this.state.failedErrorMessagesMap()[index] ?? 'Something went wrong. Try again.';
  }

  getQuestionText(questionId: string): string {
    const s = this.session();
    if (!s?.questions?.length) return 'Question';
    const q = s.questions.find((qq): qq is QuizQuestion => qq != null && (qq as QuizQuestion).id === questionId);
    return q?.text ?? 'Question';
  }

  ngOnInit(): void {
    if (this.session()) return;
    const documentId = this.route.snapshot.paramMap.get('id');
    if (!documentId) {
      this.router.navigate(['/dashboard']);
      return;
    }
    this.api.startQuiz(documentId).subscribe({
      next: s => {
        this.startError.set(null);
        this.state.setSession(s);
        this.loadQuestion(0);
      },
      error: err => {
        const message =
          err instanceof EnterpriseApiError && err.errors.length > 0
            ? err.errors[0].message
            : 'Failed to start quiz. Please try again.';
        this.startError.set(message);
      }
    });
  }

  private loadQuestion(index: number): void {
    const s = this.session();
    if (!s) return;
    if (index < 0 || index >= s.totalQuestionCount) return;
    const slot = s.questions[index];
    if (slot && typeof (slot as QuizQuestion).text === 'string') return;
    if (this.inFlight.has(index)) return;
    this.inFlight.add(index);
    this.state.addLoadingIndex(index);
    this.api.getQuizQuestionResponse(s.quizId, index).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        const body = res.body;
        const statusCode = res.status;
        if (statusCode === 202 && body?.status === 'Generating') {
          this.generatingIndexes.add(index);
          this.polling.pollQuestion(s.quizId, index).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
            next: r => this.handleQuestionResponse(index, r, s),
            error: () => this.handleQuestionError(index)
          });
          return;
        }
        if (body) this.handleQuestionResponse(index, body, s);
        else this.handleQuestionError(index);
      },
      error: () => this.handleQuestionError(index)
    });
  }

  private handleQuestionResponse(index: number, res: GetQuizQuestionResponse, s: NonNullable<QuizSession>): void {
    this.inFlight.delete(index);
    this.generatingIndexes.delete(index);
    this.state.removeLoadingIndex(index);
    const status = res.status ?? 'Ready';
    if (status === 'Ready' && res.text != null && res.options != null) {
      this.state.setQuestionAt(index, { id: res.id, text: res.text, options: res.options });
      if (index + 1 < s.totalQuestionCount) this.loadQuestion(index + 1);
    } else if (status === 'Failed') {
      this.state.addFailedIndex(index, res.errorMessage ?? undefined);
    } else {
      this.state.addLoadingIndex(index);
    }
  }

  private handleQuestionError(index: number): void {
    this.inFlight.delete(index);
    this.generatingIndexes.delete(index);
    this.state.removeLoadingIndex(index);
    this.state.addFailedIndex(index, 'Failed to load question.');
  }

  retryQuestion(index: number): void {
    this.state.retryQuestion(index);
    this.loadQuestion(index);
  }

  select(index: number): void {
    this.selectedIndex.set(index);
    const q = this.currentQuestion();
    if (q) this.answers.update(a => ({ ...a, [q.id]: index }));
  }

  next(): void {
    const nextIdx = this.currentIndex() + 1;
    this.currentIndex.set(nextIdx);
    const s = this.session();
    const q = s?.questions[nextIdx];
    this.selectedIndex.set(q && 'id' in q ? this.answers()[(q as QuizQuestion).id] ?? null : null);
    if (s && nextIdx < s.totalQuestionCount && (!s.questions[nextIdx] || typeof (s.questions[nextIdx] as QuizQuestion)?.text !== 'string'))
      this.loadQuestion(nextIdx);
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
        const q = s.questions.find((x): x is QuizQuestion => x != null && 'id' in x && (x as QuizQuestion).id === questionId);
        const optionText = q ? q.options[idx] ?? '' : '';
        return {
          questionId,
          submittedAnswer: optionText,
          submittedOptionIndex: typeof idx === 'number' ? idx : undefined,
        };
      });
    this.api.submitQuiz({ quizId: s.quizId, answers }).subscribe({
      next: r => {
        const correctCount = (r as { correctCount?: number; CorrectCount?: number }).correctCount
          ?? (r as { correctCount?: number; CorrectCount?: number }).CorrectCount ?? 0;
        const totalCount = (r as { totalCount?: number; TotalCount?: number }).totalCount
          ?? (r as { totalCount?: number; TotalCount?: number }).TotalCount ?? s.totalQuestionCount;
        const questionResults = (r as QuizResult).questionResults;
        this.state.setResult({ correctCount, totalCount, questionResults });
      },
      error: () => this.router.navigate(['/dashboard'])
    });
  }

  goDashboard(): void {
    this.startError.set(null);
    this.state.clear();
    this.router.navigate(['/dashboard']);
  }
}

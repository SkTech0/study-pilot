import { Injectable, signal, computed } from '@angular/core';
import { QuizSession, QuizResult, QuizQuestion } from '@core/services/study-pilot-api.service';

@Injectable({ providedIn: 'root' })
export class QuizStateService {
  private readonly session = signal<QuizSession | null>(null);
  private readonly result = signal<QuizResult | null>(null);
  private readonly loadingIndexes = signal<Set<number>>(new Set());
  private readonly failedIndexes = signal<Set<number>>(new Set());
  private readonly failedErrorMessages = signal<Record<number, string>>({});

  readonly currentSession = this.session.asReadonly();
  readonly currentResult = this.result.asReadonly();
  readonly loadingIndexesSet = this.loadingIndexes.asReadonly();
  readonly failedIndexesSet = this.failedIndexes.asReadonly();
  readonly failedErrorMessagesMap = this.failedErrorMessages.asReadonly();

  setSession(s: QuizSession): void {
    const total = s.totalQuestionCount ?? s.questions?.length ?? 0;
    const questions: (QuizQuestion | null)[] = Array.isArray(s.questions) && s.questions.length === total
      ? [...s.questions]
      : Array(total).fill(null);
    this.session.set({ quizId: s.quizId, totalQuestionCount: total, questions });
    this.result.set(null);
    this.loadingIndexes.set(new Set());
    this.failedIndexes.set(new Set());
    this.failedErrorMessages.set({});
  }

  setResult(r: QuizResult): void {
    this.result.set(r);
  }

  setQuestionAt(index: number, question: QuizQuestion | null): void {
    this.session.update(s => {
      if (!s || index < 0 || index >= s.questions.length) return s;
      const next = [...s.questions];
      next[index] = question;
      return { ...s, questions: next };
    });
  }

  addLoadingIndex(index: number): void {
    this.loadingIndexes.update(set => new Set(set).add(index));
  }

  removeLoadingIndex(index: number): void {
    this.loadingIndexes.update(set => {
      const next = new Set(set);
      next.delete(index);
      return next;
    });
  }

  addFailedIndex(index: number, errorMessage?: string): void {
    this.failedIndexes.update(set => new Set(set).add(index));
    if (errorMessage != null) {
      this.failedErrorMessages.update(m => ({ ...m, [index]: errorMessage }));
    }
  }

  removeFailedIndex(index: number): void {
    this.failedIndexes.update(set => {
      const next = new Set(set);
      next.delete(index);
      return next;
    });
    this.failedErrorMessages.update(m => {
      const { [index]: _, ...rest } = m;
      return rest;
    });
  }

  retryQuestion(index: number): void {
    this.removeFailedIndex(index);
  }

  clear(): void {
    this.session.set(null);
    this.result.set(null);
    this.loadingIndexes.set(new Set());
    this.failedIndexes.set(new Set());
    this.failedErrorMessages.set({});
  }
}

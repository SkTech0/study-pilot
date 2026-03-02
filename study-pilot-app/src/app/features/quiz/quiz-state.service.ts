import { Injectable, signal, computed } from '@angular/core';
import { QuizSession, QuizResult } from '@core/services/study-pilot-api.service';

@Injectable({ providedIn: 'root' })
export class QuizStateService {
  private readonly session = signal<QuizSession | null>(null);
  private readonly result = signal<QuizResult | null>(null);
  readonly currentSession = this.session.asReadonly();
  readonly currentResult = this.result.asReadonly();

  setSession(s: QuizSession): void {
    this.session.set(s);
    this.result.set(null);
  }

  setResult(r: QuizResult): void {
    this.result.set(r);
  }

  clear(): void {
    this.session.set(null);
    this.result.set(null);
  }
}

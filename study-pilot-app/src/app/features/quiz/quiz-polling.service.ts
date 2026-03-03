import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { timer, switchMap, takeWhile } from 'rxjs';
import { StudyPilotApiService, GetQuizQuestionResponse } from '@core/services/study-pilot-api.service';

const POLL_INTERVAL_MS = 3000;

@Injectable({ providedIn: 'root' })
export class QuizPollingService {
  private readonly api = inject(StudyPilotApiService);

  pollQuestion(quizId: string, questionIndex: number): Observable<GetQuizQuestionResponse> {
    return timer(0, POLL_INTERVAL_MS).pipe(
      switchMap(() => this.api.getQuizStatus(quizId, questionIndex)),
      takeWhile((res) => res.status === 'Generating', true)
    );
  }
}

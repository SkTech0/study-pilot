import { Injectable, inject } from '@angular/core';
import { Observable, interval, switchMap, takeWhile, tap } from 'rxjs';
import { StudyPilotApiService, DocumentItem } from '../services/study-pilot-api.service';

@Injectable({ providedIn: 'root' })
export class DocumentPollingService {
  private readonly api = inject(StudyPilotApiService);

  pollUntilCompleted(intervalMs: number = 5000): Observable<DocumentItem[]> {
    return interval(intervalMs).pipe(
      switchMap(() => this.api.getDocuments()),
      takeWhile(docs => docs.some(d => d.status === 'Pending' || d.status === 'Processing'), true)
    );
  }
}

import { Injectable, inject } from '@angular/core';
import { Observable, interval, switchMap, takeWhile, catchError, of, map } from 'rxjs';
import { StudyPilotApiService, DocumentItem } from '../services/study-pilot-api.service';
import { NetworkStatusService } from './network-status.service';

const MAX_CONSECUTIVE_ERRORS = 3;

@Injectable({ providedIn: 'root' })
export class DocumentPollingService {
  private readonly api = inject(StudyPilotApiService);
  private readonly network = inject(NetworkStatusService);

  pollUntilCompleted(intervalMs: number = 5000): Observable<DocumentItem[]> {
    let consecutiveErrors = 0;
    return interval(intervalMs).pipe(
      takeWhile(() => this.network.isOnline(), true),
      switchMap(() =>
        this.api.getDocuments().pipe(
          catchError(() => {
            consecutiveErrors++;
            return of([] as DocumentItem[]);
          })
        )
      ),
      map(docs => {
        if (docs.length > 0) consecutiveErrors = 0;
        return docs;
      }),
      takeWhile(docs => {
        if (docs.length > 0)
          return docs.some(d => d.status === 'Pending' || d.status === 'Processing');
        return consecutiveErrors < MAX_CONSECUTIVE_ERRORS;
      }, true)
    );
  }
}

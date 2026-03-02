import { Component, inject, ChangeDetectionStrategy, signal, OnInit, OnDestroy } from '@angular/core';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { Subscription, filter } from 'rxjs';
import { StudyPilotApiService, DocumentItem } from '@core/services/study-pilot-api.service';
import { DocumentPollingService } from '@core/services/document-polling.service';

@Component({
  selector: 'app-document-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="p-4 sm:p-6 max-w-4xl mx-auto">
      <div class="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 mb-6">
        <div>
          <h1 class="text-2xl font-semibold text-gray-900">Documents</h1>
          <p class="mt-0.5 text-sm text-gray-500">Upload PDFs and start quizzes when processing is complete</p>
        </div>
        <a routerLink="/documents/upload" class="btn-primary shrink-0">Upload document</a>
      </div>
      @if (loading()) {
        <div class="card">
          <div class="space-y-3">
            @for (i of [1,2,3,4]; track i) {
              <div class="flex items-center justify-between py-3 animate-pulse">
                <span class="h-4 bg-gray-200 rounded w-1/2"></span>
                <span class="h-6 bg-gray-100 rounded-full w-20"></span>
              </div>
            }
          </div>
        </div>
      } @else {
        <div class="card">
          <ul class="divide-y divide-gray-200">
            @for (doc of documents(); track doc.id) {
              <li class="py-4 flex flex-col gap-2">
                <div class="flex flex-col sm:flex-row sm:justify-between sm:items-center gap-2">
                  <span class="font-medium text-gray-900 truncate">{{ doc.fileName }}</span>
                  <div class="flex items-center gap-2 flex-wrap">
                    @if (doc.status === 'Completed') {
                      <a [routerLink]="['/quiz', doc.id]" class="btn-primary text-sm">Start quiz</a>
                    }
                    <span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium"
                          [class.bg-yellow-100]="doc.status === 'Pending' || doc.status === 'Processing'"
                          [class.text-yellow-800]="doc.status === 'Pending' || doc.status === 'Processing'"
                          [class.bg-green-100]="doc.status === 'Completed'"
                          [class.text-green-800]="doc.status === 'Completed'"
                          [class.bg-red-100]="doc.status === 'Failed'"
                          [class.text-red-800]="doc.status === 'Failed'">
                      {{ doc.status }}
                    </span>
                  </div>
                </div>
                @if (doc.status === 'Failed' && doc.failureReason) {
                  <p class="text-sm text-red-700 bg-red-50 rounded-lg px-3 py-2" role="alert">
                    {{ doc.failureReason }}
                  </p>
                }
              </li>
            }
          </ul>
          @if (documents().length === 0) {
            <div class="text-center py-10">
              <p class="text-gray-500">No documents yet.</p>
              <a routerLink="/documents/upload" class="btn-primary mt-3 inline-block">Upload your first document</a>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class DocumentListComponent implements OnInit, OnDestroy {
  private readonly api = inject(StudyPilotApiService);
  private readonly polling = inject(DocumentPollingService);
  private readonly router = inject(Router);
  documents = signal<DocumentItem[]>([]);
  loading = signal(true);
  private pollingSub: Subscription | null = null;
  private sub = this.router.events.pipe(
    filter((e): e is NavigationEnd => e instanceof NavigationEnd),
    filter(e => e.urlAfterRedirects === '/documents' || e.urlAfterRedirects.startsWith('/documents?'))
  ).subscribe(() => this.loadDocuments());

  ngOnInit(): void {
    this.loadDocuments();
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
    this.pollingSub?.unsubscribe();
  }

  private loadDocuments(): void {
    this.loading.set(true);
    this.api.getDocuments().subscribe({
      next: list => {
        this.documents.set(list);
        this.loading.set(false);
        if (list.some(d => d.status === 'Pending' || d.status === 'Processing')) {
          this.pollingSub?.unsubscribe();
          this.pollingSub = this.polling.pollUntilCompleted(5000).subscribe(list => this.documents.set(list));
        }
      },
      error: () => this.loading.set(false)
    });
  }
}

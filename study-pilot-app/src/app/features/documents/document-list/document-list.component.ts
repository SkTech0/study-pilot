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
    <div class="p-4">
      <div class="flex justify-between items-center mb-4">
        <h1 class="text-xl font-semibold">Documents</h1>
        <a routerLink="/documents/upload" class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700">Upload</a>
      </div>
      @if (loading()) {
        <p class="text-gray-600">Loading...</p>
      } @else {
        <ul class="divide-y">
          @for (doc of documents(); track doc.id) {
            <li class="py-3 flex justify-between items-center gap-2">
              <span>{{ doc.fileName }}</span>
              <div class="flex items-center gap-2">
                @if (doc.status === 'Completed') {
                  <a [routerLink]="['/quiz', doc.id]" class="text-blue-600 hover:underline text-sm">Start quiz</a>
                }
                <span class="px-2 py-1 rounded text-sm"
                      [class.bg-yellow-100]="doc.status === 'Pending' || doc.status === 'Processing'"
                      [class.bg-green-100]="doc.status === 'Completed'"
                      [class.bg-red-100]="doc.status === 'Failed'">
                  {{ doc.status }}
                </span>
              </div>
            </li>
          }
        </ul>
        @if (documents().length === 0) {
          <p class="text-gray-500">No documents yet. Upload one to get started.</p>
        }
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

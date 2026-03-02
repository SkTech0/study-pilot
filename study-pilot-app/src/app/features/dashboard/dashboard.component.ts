import { Component, inject, ChangeDetectionStrategy, signal, computed, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StudyPilotApiService, DocumentItem, WeakTopic } from '@core/services/study-pilot-api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="p-4 max-w-4xl mx-auto">
      <h1 class="text-2xl font-semibold mb-6">Dashboard</h1>
      <div class="grid gap-6 md:grid-cols-2">
        <div class="rounded border p-4 bg-white shadow">
          <h2 class="font-medium mb-2">Quick actions</h2>
          <a routerLink="/documents/upload" class="inline-block bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 mb-2">Upload document</a>
          <a routerLink="/documents" class="block text-blue-600 hover:underline">Documents (upload &amp; start quiz)</a>
        </div>
        <div class="rounded border p-4 bg-white shadow">
          <h2 class="font-medium mb-2">AI health</h2>
          <span class="px-2 py-1 rounded text-sm" [class]="aiHealthClass()">{{ aiHealth() }}</span>
        </div>
      </div>
      <div class="mt-6 rounded border p-4 bg-white shadow">
        <h2 class="font-medium mb-2">Recent documents</h2>
        <ul class="divide-y">
          @for (doc of recentDocs(); track doc.id) {
            <li class="py-2 flex justify-between">
              <span>{{ doc.fileName }}</span>
              <span class="text-sm text-gray-500">{{ doc.status }}</span>
            </li>
          }
        </ul>
        @if (recentDocs().length === 0 && !loadingDocs()) {
          <p class="text-gray-500 text-sm">No documents yet.</p>
        }
        <a routerLink="/documents" class="text-blue-600 text-sm hover:underline mt-2 inline-block">View all</a>
      </div>
      <div class="mt-6 rounded border p-4 bg-white shadow">
        <h2 class="font-medium mb-2">Weak topics</h2>
        <ul class="divide-y">
          @for (t of weakTopics(); track t.conceptId) {
            <li class="py-2 flex justify-between">
              <span>{{ t.conceptName }}</span>
              <span class="text-sm">{{ t.masteryScore }}%</span>
            </li>
          }
        </ul>
        @if (weakTopics().length === 0 && !loadingWeak()) {
          <p class="text-gray-500 text-sm">No weak topics yet.</p>
        }
        <a routerLink="/progress" class="text-blue-600 text-sm hover:underline mt-2 inline-block">View all</a>
      </div>
    </div>
  `
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(StudyPilotApiService);
  recentDocs = signal<DocumentItem[]>([]);
  weakTopics = signal<WeakTopic[]>([]);
  aiHealth = signal<string>('Unknown');
  loadingDocs = signal(true);
  loadingWeak = signal(true);
  aiHealthClass = computed(() => {
    const h = this.aiHealth();
    return h === 'Healthy' ? 'bg-green-100 text-green-800' : h === 'Degraded' ? 'bg-yellow-100 text-yellow-800' : 'bg-red-100 text-red-800';
  });

  ngOnInit(): void {
    this.api.getDocuments().subscribe({
      next: list => {
        this.recentDocs.set(list.slice(0, 5));
        this.loadingDocs.set(false);
      },
      error: () => this.loadingDocs.set(false)
    });
    this.api.getWeakTopics().subscribe({
      next: list => {
        this.weakTopics.set(list.slice(0, 5));
        this.loadingWeak.set(false);
      },
      error: () => this.loadingWeak.set(false)
    });
    this.api.getAIHealth().subscribe({
      next: r => this.aiHealth.set(r.status),
      error: () => this.aiHealth.set('Unhealthy')
    });
  }
}

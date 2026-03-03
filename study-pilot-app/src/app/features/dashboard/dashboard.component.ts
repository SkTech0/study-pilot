import { Component, inject, ChangeDetectionStrategy, signal, computed, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StudyPilotApiService, DocumentItem, WeakTopic } from '@core/services/study-pilot-api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="p-4 sm:p-6 max-w-4xl mx-auto">
      <div class="mb-8">
        <h1 class="text-2xl sm:text-3xl font-semibold text-gray-900">Your dashboard</h1>
        <p class="mt-1 text-gray-500">Your study materials and progress at a glance</p>
      </div>
      <div class="grid gap-6 md:grid-cols-2">
        <div class="card">
          <h2 class="text-lg font-medium text-gray-900 mb-3">Quick actions</h2>
          <div class="flex flex-col gap-2">
            <a routerLink="/documents/upload" class="btn-primary text-center">Upload a PDF</a>
            <a routerLink="/documents" class="btn-secondary text-center">My documents &amp; quizzes</a>
            <a routerLink="/chat" class="btn-secondary text-center">Chat (RAG)</a>
            <a routerLink="/chat/stream" class="btn-secondary text-center">Chat (Stream)</a>
            <a routerLink="/learning" class="btn-secondary text-center">Learning dashboard</a>
            <a routerLink="/tutor" class="btn-secondary text-center">Start tutor session</a>
          </div>
        </div>
        <div class="card">
          <h2 class="text-lg font-medium text-gray-900 mb-2">AI health</h2>
          <span class="inline-flex items-center px-2.5 py-1 rounded-full text-sm font-medium" [class]="aiHealthClass()">{{ aiHealth() }}</span>
        </div>
      </div>
      <div class="mt-6 card">
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-lg font-medium text-gray-900">Recent documents</h2>
          <a routerLink="/documents" class="text-sm font-medium text-blue-600 hover:text-blue-700">View all</a>
        </div>
        @if (loadingDocs()) {
          <ul class="divide-y divide-gray-200">
            @for (i of [1,2,3]; track i) {
              <li class="py-3 animate-pulse"><span class="block h-4 bg-gray-200 rounded w-3/4"></span><span class="block h-3 bg-gray-100 rounded w-16 mt-1"></span></li>
            }
          </ul>
        } @else {
          <ul class="divide-y divide-gray-200">
            @for (doc of recentDocs(); track doc.id) {
              <li class="py-3 flex justify-between items-center gap-2">
                <span class="text-gray-900 truncate">{{ doc.fileName }}</span>
                <span class="shrink-0 px-2 py-0.5 rounded text-xs font-medium"
                      [class.bg-yellow-100]="doc.status === 'Pending' || doc.status === 'Processing'"
                      [class.text-yellow-800]="doc.status === 'Pending' || doc.status === 'Processing'"
                      [class.bg-green-100]="doc.status === 'Completed'"
                      [class.text-green-800]="doc.status === 'Completed'"
                      [class.bg-red-100]="doc.status === 'Failed'"
                      [class.text-red-800]="doc.status === 'Failed'">{{ doc.status === 'Processing' ? 'Analyzing…' : doc.status === 'Pending' ? 'In queue' : doc.status === 'Completed' ? 'Ready' : doc.status }}</span>
              </li>
            }
          </ul>
          @if (recentDocs().length === 0) {
            <div class="text-center py-6">
              <p class="text-gray-500 text-sm">You haven't uploaded any documents yet.</p>
              <p class="text-gray-400 text-xs mt-1">Upload a PDF to generate quizzes and track your progress.</p>
              <a routerLink="/documents/upload" class="btn-primary mt-3 inline-block text-sm">Upload your first document</a>
            </div>
          }
        }
      </div>
      <div class="mt-6 card">
        <div class="flex items-center justify-between mb-3">
          <h2 class="text-lg font-medium text-gray-900">Weak topics</h2>
          <a routerLink="/progress" class="text-sm font-medium text-blue-600 hover:text-blue-700">View all</a>
        </div>
        @if (loadingWeak()) {
          <ul class="space-y-2">
            @for (i of [1,2,3]; track i) {
              <li class="animate-pulse h-10 bg-gray-100 rounded"></li>
            }
          </ul>
        } @else {
          <ul class="divide-y divide-gray-200">
            @for (t of weakTopics(); track t.conceptId) {
              <li class="py-3 flex justify-between items-center gap-2">
                <span class="text-gray-900">{{ t.name }}</span>
                <span class="text-sm font-medium text-gray-600">{{ t.masteryScore }}%</span>
              </li>
            }
          </ul>
          @if (weakTopics().length === 0) {
            <div class="text-center py-6">
              <p class="text-gray-500 text-sm">No weak topics yet.</p>
              <p class="text-gray-400 text-xs mt-1">Complete a quiz to see which topics need more practice.</p>
            </div>
          }
        }
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

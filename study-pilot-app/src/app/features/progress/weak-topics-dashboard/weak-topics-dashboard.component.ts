import { Component, inject, ChangeDetectionStrategy, signal, OnInit } from '@angular/core';
import { StudyPilotApiService, WeakTopic } from '@core/services/study-pilot-api.service';

@Component({
  selector: 'app-weak-topics-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-4 sm:p-6 max-w-4xl mx-auto">
      <div class="mb-6">
        <h1 class="text-2xl font-semibold text-gray-900">Progress &amp; weak topics</h1>
        <p class="mt-1 text-sm text-gray-500">Topics to review based on your quiz performance</p>
      </div>
      @if (loading()) {
        <div class="card space-y-4">
          @for (i of [1,2,3,4,5]; track i) {
            <div class="animate-pulse flex items-center gap-4">
              <span class="h-4 bg-gray-200 rounded flex-1 max-w-[200px]"></span>
              <span class="h-2 bg-gray-100 rounded w-24"></span>
              <span class="h-4 bg-gray-100 rounded w-10"></span>
            </div>
          }
        </div>
      } @else {
        <div class="card">
          <ul class="space-y-4">
            @for (t of topics(); track t.conceptId) {
              <li class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
                <span class="font-medium text-gray-900">{{ t.conceptName }}</span>
                <div class="flex items-center gap-3">
                  <div class="w-24 sm:w-32 h-2.5 bg-gray-200 rounded-full overflow-hidden" role="progressbar" [attr.aria-valuenow]="t.masteryScore" aria-valuemin="0" aria-valuemax="100">
                    <div class="h-full bg-blue-600 rounded-full transition-all" [style.width.%]="t.masteryScore"></div>
                  </div>
                  <span class="text-sm font-medium text-gray-600 w-10">{{ t.masteryScore }}%</span>
                </div>
              </li>
            }
          </ul>
          @if (topics().length === 0) {
            <div class="text-center py-10 text-gray-500">
              <p>No weak topics yet.</p>
              <p class="mt-1 text-sm">Complete quizzes to see your progress here.</p>
            </div>
          }
        </div>
      }
    </div>
  `
})
export class WeakTopicsDashboardComponent implements OnInit {
  private readonly api = inject(StudyPilotApiService);
  topics = signal<WeakTopic[]>([]);
  loading = signal(true);

  ngOnInit(): void {
    this.api.getWeakTopics().subscribe({
      next: list => {
        this.topics.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}

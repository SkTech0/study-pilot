import { Component, inject, ChangeDetectionStrategy, signal, OnInit } from '@angular/core';
import { StudyPilotApiService, LearningWeakTopicItem } from '@core/services/study-pilot-api.service';

@Component({
  selector: 'app-learning-weak-topics',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card">
      <h2 class="text-lg font-medium text-gray-900 mb-4">Weak topics</h2>
      @if (loading()) {
        <ul class="space-y-3">
          @for (i of [1,2,3,4]; track i) {
            <li class="animate-pulse flex items-center gap-3">
              <span class="h-4 bg-gray-200 rounded flex-1 max-w-[180px]"></span>
              <span class="h-2 bg-gray-100 rounded w-20"></span>
              <span class="h-4 bg-gray-100 rounded w-10"></span>
            </li>
          }
        </ul>
      } @else {
        <ul class="space-y-3">
          @for (t of topics(); track t.conceptId) {
            <li class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2">
              <span class="font-medium text-gray-900">{{ t.conceptName }}</span>
              <div class="flex items-center gap-3">
                <div class="w-24 sm:w-32 h-2.5 bg-gray-200 rounded-full overflow-hidden" role="progressbar" [attr.aria-valuenow]="t.masteryScore" aria-valuemin="0" aria-valuemax="100">
                  <div class="h-full bg-amber-500 rounded-full transition-all" [style.width.%]="t.masteryScore"></div>
                </div>
                <span class="text-sm font-medium text-gray-600 w-10">{{ t.masteryScore }}%</span>
              </div>
            </li>
          }
        </ul>
        @if (topics().length === 0) {
          <p class="text-gray-500 text-sm py-4">No weak topics. Keep practicing to maintain strength.</p>
        }
      }
    </div>
  `
})
export class WeakTopicsComponent implements OnInit {
  private readonly api = inject(StudyPilotApiService);
  topics = signal<LearningWeakTopicItem[]>([]);
  loading = signal(true);

  ngOnInit(): void {
    this.api.getLearningWeakTopics(20).subscribe({
      next: res => {
        this.topics.set(res.topics ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}

import { Component, inject, ChangeDetectionStrategy, signal, OnInit } from '@angular/core';
import { StudyPilotApiService, WeakTopic } from '@core/services/study-pilot-api.service';

@Component({
  selector: 'app-weak-topics-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-4">
      <h1 class="text-xl font-semibold mb-4">Weak topics</h1>
      @if (loading()) {
        <p class="text-gray-600">Loading...</p>
      } @else {
        <ul class="space-y-3">
          @for (t of topics(); track t.conceptId) {
            <li class="border rounded p-3 flex justify-between items-center">
              <span class="font-medium">{{ t.conceptName }}</span>
              <div class="flex items-center gap-2">
                <div class="w-24 h-2 bg-gray-200 rounded overflow-hidden">
                  <div class="h-full bg-blue-600 rounded" [style.width.%]="t.masteryScore"></div>
                </div>
                <span class="text-sm text-gray-600">{{ t.masteryScore }}%</span>
              </div>
            </li>
          }
        </ul>
        @if (topics().length === 0) {
          <p class="text-gray-500">No weak topics yet. Complete quizzes to see your progress.</p>
        }
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

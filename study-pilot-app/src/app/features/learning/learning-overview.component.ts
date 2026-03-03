import { Component, inject, ChangeDetectionStrategy, signal, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { StudyPilotApiService, LearningOverview } from '@core/services/study-pilot-api.service';

@Component({
  selector: 'app-learning-overview',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe],
  template: `
    <div class="card">
      <h2 class="text-lg font-medium text-gray-900 mb-4">Learning overview</h2>
      @if (loading()) {
        <div class="animate-pulse space-y-3">
          <div class="h-6 bg-gray-200 rounded w-1/3"></div>
          <div class="h-4 bg-gray-100 rounded w-2/3"></div>
          <div class="grid grid-cols-3 gap-2 mt-4">
            @for (i of [1,2,3]; track i) { <div class="h-16 bg-gray-100 rounded"></div> }
          </div>
        </div>
      } @else if (overview()) {
        <div class="space-y-4">
          <p class="text-gray-600 text-sm">
            <span class="font-medium text-gray-900">{{ overview()!.totalConcepts }}</span> concepts tracked ·
            Average mastery <span class="font-medium">{{ overview()!.averageMastery | number:'1.0-0' }}%</span>
          </p>
          <div class="grid grid-cols-3 gap-3">
            <div class="rounded-lg bg-amber-50 p-3 text-center">
              <span class="text-2xl font-semibold text-amber-800">{{ overview()!.weakCount }}</span>
              <p class="text-xs text-amber-700 mt-0.5">Weak</p>
            </div>
            <div class="rounded-lg bg-blue-50 p-3 text-center">
              <span class="text-2xl font-semibold text-blue-800">{{ overview()!.mediumCount }}</span>
              <p class="text-xs text-blue-700 mt-0.5">Medium</p>
            </div>
            <div class="rounded-lg bg-green-50 p-3 text-center">
              <span class="text-2xl font-semibold text-green-800">{{ overview()!.strongCount }}</span>
              <p class="text-xs text-green-700 mt-0.5">Strong</p>
            </div>
          </div>
          @if (overview()!.distribution.length) {
            <div class="pt-2 border-t border-gray-100">
              <p class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-2">Distribution</p>
              <div class="flex gap-2 flex-wrap">
                @for (d of overview()!.distribution; track d.bucket) {
                  <span class="px-2 py-1 rounded bg-gray-100 text-sm text-gray-700">{{ d.bucket }}: {{ d.count }}</span>
                }
              </div>
            </div>
          }
        </div>
      } @else {
        <p class="text-gray-500 text-sm">No mastery data yet. Complete quizzes to see your overview.</p>
      }
    </div>
  `
})
export class LearningOverviewComponent implements OnInit {
  private readonly api = inject(StudyPilotApiService);
  overview = signal<LearningOverview | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.api.getLearningOverview().subscribe({
      next: data => {
        this.overview.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}

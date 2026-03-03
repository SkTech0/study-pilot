import { Component, inject, ChangeDetectionStrategy, signal, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { StudyPilotApiService, LearningProgressResponse } from '@core/services/study-pilot-api.service';

@Component({
  selector: 'app-progress-chart',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe],
  template: `
    <div class="card">
      <h2 class="text-lg font-medium text-gray-900 mb-4">Progress</h2>
      @if (loading()) {
        <div class="animate-pulse space-y-4">
          <div class="h-20 bg-gray-100 rounded"></div>
          <div class="h-32 bg-gray-100 rounded"></div>
        </div>
      } @else if (progress()) {
        <div class="space-y-4">
          <p class="text-sm text-gray-600">
            Improvement trend (avg mastery): <span class="font-semibold text-gray-900">{{ progress()!.improvementTrend | number:'1.0-1' }}%</span>
          </p>
          <div class="grid sm:grid-cols-2 gap-4">
            <div>
              <h3 class="text-sm font-medium text-green-800 mb-2">Strongest</h3>
              <ul class="space-y-1.5">
                @for (c of progress()!.strongestConcepts; track c.conceptId) {
                  <li class="flex justify-between items-center text-sm">
                    <span class="text-gray-900 truncate mr-2">{{ c.name }}</span>
                    <span class="font-medium text-green-700 shrink-0">{{ c.masteryScore }}%</span>
                  </li>
                }
              </ul>
              @if (progress()!.strongestConcepts.length === 0) {
                <p class="text-gray-500 text-sm">—</p>
              }
            </div>
            <div>
              <h3 class="text-sm font-medium text-amber-800 mb-2">Weakest</h3>
              <ul class="space-y-1.5">
                @for (c of progress()!.weakestConcepts; track c.conceptId) {
                  <li class="flex justify-between items-center text-sm">
                    <span class="text-gray-900 truncate mr-2">{{ c.name }}</span>
                    <span class="font-medium text-amber-700 shrink-0">{{ c.masteryScore }}%</span>
                  </li>
                }
              </ul>
              @if (progress()!.weakestConcepts.length === 0) {
                <p class="text-gray-500 text-sm">—</p>
              }
            </div>
          </div>
        </div>
      } @else {
        <p class="text-gray-500 text-sm">No progress data yet.</p>
      }
    </div>
  `
})
export class ProgressChartComponent implements OnInit {
  private readonly api = inject(StudyPilotApiService);
  progress = signal<LearningProgressResponse | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.api.getLearningProgress().subscribe({
      next: data => {
        this.progress.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}

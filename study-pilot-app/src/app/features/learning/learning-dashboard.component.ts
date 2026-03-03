import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LearningOverviewComponent } from './learning-overview.component';
import { WeakTopicsComponent } from './weak-topics.component';
import { ProgressChartComponent } from './progress-chart.component';
import { StudySuggestionsComponent } from './study-suggestions.component';

@Component({
  selector: 'app-learning-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LearningOverviewComponent, WeakTopicsComponent, ProgressChartComponent, StudySuggestionsComponent],
  template: `
    <div class="p-4 sm:p-6 max-w-4xl mx-auto">
      <div class="mb-6 flex items-center justify-between">
        <div>
          <h1 class="text-2xl font-semibold text-gray-900">Learning dashboard</h1>
          <p class="mt-1 text-sm text-gray-500">Mastery overview, weak topics, and progress</p>
        </div>
        <a routerLink="/dashboard" class="text-sm font-medium text-blue-600 hover:text-blue-700">← Dashboard</a>
      </div>
      <div class="space-y-6">
        <app-study-suggestions />
        <app-learning-overview />
        <app-learning-weak-topics />
        <app-progress-chart />
      </div>
    </div>
  `
})
export class LearningDashboardComponent {}

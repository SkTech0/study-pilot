import { Component, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StudyPilotApiService, StudySuggestionItem } from '@core/services/study-pilot-api.service';

@Component({
  selector: 'app-study-suggestions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <div class="rounded-lg border border-gray-200 bg-blue-50/30 p-4">
      <h2 class="text-lg font-medium text-gray-900 mb-2">Recommended next study session</h2>
      @if (loading()) {
        <p class="text-sm text-gray-500">Loading…</p>
      } @else if (suggestions().length > 0) {
        @for (s of suggestions(); track s.title) {
          <p class="text-sm text-gray-700">{{ s.description }}</p>
          <a routerLink="/tutor" class="btn-primary mt-3 inline-block text-sm">Start tutor session</a>
        }
      } @else {
        <p class="text-sm text-gray-500">Complete quizzes or upload documents to get personalized suggestions.</p>
        <a routerLink="/tutor" class="btn-secondary mt-3 inline-block text-sm">Start tutor session</a>
      }
    </div>
  `,
})
export class StudySuggestionsComponent implements OnInit {
  private readonly api = inject(StudyPilotApiService);
  suggestions = signal<StudySuggestionItem[]>([]);
  loading = signal(true);

  ngOnInit(): void {
    this.api.getStudySuggestions().subscribe({
      next: (res) => {
        this.suggestions.set(res.suggestions ?? []);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}

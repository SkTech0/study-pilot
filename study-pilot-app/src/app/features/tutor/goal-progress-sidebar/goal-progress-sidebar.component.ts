import { Component, input, ChangeDetectionStrategy } from '@angular/core';

export interface TutorGoalItem {
  goalId: string;
  conceptId: string;
  conceptName: string;
  goalType: string;
  priority: number;
}

@Component({
  selector: 'app-goal-progress-sidebar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rounded-lg border border-gray-200 bg-gray-50/50 p-3">
      <h3 class="text-sm font-medium text-gray-700 mb-2">Session goals</h3>
      @if (goals().length === 0) {
        <p class="text-xs text-gray-500">No goals yet. Start the session to generate goals from your weak topics.</p>
      } @else {
        <ul class="space-y-1.5">
          @for (g of goals(); track g.goalId) {
            <li class="text-xs text-gray-700 flex items-center gap-2">
              <span class="shrink-0 w-2 h-2 rounded-full bg-blue-500"></span>
              <span class="font-medium">{{ g.conceptName }}</span>
              <span class="text-gray-500">({{ g.goalType }})</span>
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class GoalProgressSidebarComponent {
  goals = input<TutorGoalItem[]>([]);
}

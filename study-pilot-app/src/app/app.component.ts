import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { ToastComponent } from './shared/components/toast/toast.component';
import { AuthService } from './core/auth/auth.service';
import { ErrorBannerService } from './core/services/error-banner.service';

@Component({
  selector: 'app-root',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastComponent],
  template: `
    @if (banner.currentSystemError(); as sys) {
      <div class="bg-amber-600 text-white px-4 py-3 flex items-center justify-between gap-4 shadow-sm" role="alert">
        <span class="flex-1 min-w-0">{{ sys.message }}@if (sys.correlationId) { <span class="opacity-90 text-sm">(Ref: {{ sys.correlationId }})</span> }</span>
        <button type="button" (click)="banner.clear()" class="shrink-0 font-medium underline hover:no-underline focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-amber-600 rounded">Dismiss</button>
      </div>
    }
    @if (auth.token) {
      <nav class="sticky top-0 z-40 border-b border-gray-200 bg-white shadow-sm" aria-label="Main">
        <div class="mx-auto max-w-6xl px-4 sm:px-6">
            <div class="flex h-14 items-center justify-between gap-4">
            <div class="flex items-center gap-4">
              <span class="text-sm font-semibold text-gray-800 hidden sm:inline">StudyPilot</span>
              <div class="flex items-center gap-1">
              <a routerLink="/dashboard" routerLinkActive="bg-blue-50 text-blue-700 font-medium" [routerLinkActiveOptions]="{ exact: true }"
                 class="rounded-lg px-3 py-2 text-sm text-gray-700 hover:bg-gray-100 hover:text-gray-900 transition-colors">Dashboard</a>
              <a routerLink="/documents" routerLinkActive="bg-blue-50 text-blue-700 font-medium" [routerLinkActiveOptions]="{ paths: 'subset', matrixParams: 'ignored', queryParams: 'ignored', fragment: 'ignored' }"
                 class="rounded-lg px-3 py-2 text-sm text-gray-700 hover:bg-gray-100 hover:text-gray-900 transition-colors">Documents</a>
              <a routerLink="/progress" routerLinkActive="bg-blue-50 text-blue-700 font-medium"
                 class="rounded-lg px-3 py-2 text-sm text-gray-700 hover:bg-gray-100 hover:text-gray-900 transition-colors">Progress</a>
              </div>
            </div>
            <button type="button" (click)="auth.logout()"
                    class="rounded-lg px-3 py-2 text-sm text-gray-600 hover:bg-gray-100 hover:text-gray-900 font-medium transition-colors focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2">
              Logout
            </button>
          </div>
        </div>
      </nav>
    }
    <main class="min-h-screen">
      <router-outlet />
    </main>
    <app-toast />
  `
})
export class AppComponent {
  readonly auth = inject(AuthService);
  readonly banner = inject(ErrorBannerService);
}

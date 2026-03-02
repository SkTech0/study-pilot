import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { ToastComponent } from './shared/components/toast/toast.component';
import { AuthService } from './core/auth/auth.service';
import { ErrorBannerService } from './core/services/error-banner.service';

@Component({
  selector: 'app-root',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, ToastComponent],
  template: `
    @if (banner.currentSystemError(); as sys) {
      <div class="bg-red-600 text-white px-4 py-2 flex items-center justify-between">
        <span>{{ sys.message }}@if (sys.correlationId) { (Ref: {{ sys.correlationId }}) }</span>
        <button type="button" (click)="banner.clear()" class="underline">Dismiss</button>
      </div>
    }
    @if (auth.token) {
      <nav class="border-b bg-white px-4 py-2 flex gap-4 items-center">
        <a routerLink="/dashboard" class="text-blue-600 hover:underline">Dashboard</a>
        <a routerLink="/documents" class="text-blue-600 hover:underline">Documents</a>
        <a routerLink="/progress" class="text-blue-600 hover:underline">Progress</a>
        <a routerLink="/quiz/start" class="text-blue-600 hover:underline">Quiz</a>
        <button type="button" (click)="auth.logout()" class="text-gray-600 hover:underline ml-auto">Logout</button>
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

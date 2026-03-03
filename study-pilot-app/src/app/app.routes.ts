import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: 'auth', loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES) },
  { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent), canActivate: [authGuard] },
  { path: 'documents', loadChildren: () => import('./features/documents/documents.routes').then(m => m.DOCUMENTS_ROUTES) },
  { path: 'chat', loadChildren: () => import('./features/chat/chat.routes').then(m => m.CHAT_ROUTES) },
  { path: 'quiz/:id', loadComponent: () => import('./features/quiz/quiz-player/quiz-player.component').then(m => m.QuizPlayerComponent), canActivate: [authGuard] },
  { path: 'progress', loadComponent: () => import('./features/progress/weak-topics-dashboard/weak-topics-dashboard.component').then(m => m.WeakTopicsDashboardComponent), canActivate: [authGuard] },
  { path: 'learning', loadChildren: () => import('./features/learning/learning.routes').then(m => m.LEARNING_ROUTES) },
  { path: 'tutor', loadChildren: () => import('./features/tutor/tutor.routes').then(m => m.TUTOR_ROUTES) },
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: 'dashboard' }
];

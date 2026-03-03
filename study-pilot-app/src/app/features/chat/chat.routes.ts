import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const CHAT_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./chat-page/chat-page.component').then(m => m.ChatPageComponent),
    canActivate: [authGuard],
  },
  {
    path: 'stream',
    loadComponent: () => import('./chat-stream/chat-stream.component').then(m => m.ChatStreamComponent),
    canActivate: [authGuard],
  },
];

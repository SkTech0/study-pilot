import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const TUTOR_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./tutor-session-page/tutor-session-page.component').then(m => m.TutorSessionPageComponent),
    canActivate: [authGuard],
  },
];

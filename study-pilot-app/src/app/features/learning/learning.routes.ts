import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const LEARNING_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./learning-dashboard.component').then(m => m.LearningDashboardComponent), canActivate: [authGuard] }
];

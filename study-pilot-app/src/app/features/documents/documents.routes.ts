import { Routes } from '@angular/router';
import { authGuard } from '../../core/guards/auth.guard';

export const DOCUMENTS_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./document-list/document-list.component').then(m => m.DocumentListComponent), canActivate: [authGuard] },
  { path: 'upload', loadComponent: () => import('./upload-document/upload-document.component').then(m => m.UploadDocumentComponent), canActivate: [authGuard] }
];

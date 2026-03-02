import { Component, inject, ChangeDetectionStrategy, signal } from '@angular/core';
import { Router } from '@angular/router';
import { StudyPilotApiService } from '@core/services/study-pilot-api.service';
import { ToastService } from '@shared/services/toast.service';
import { EnterpriseApiError } from '@core/http/enterprise-api-error';

@Component({
  selector: 'app-upload-document',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-4 sm:p-6 max-w-lg mx-auto">
      <div class="mb-6">
        <h1 class="text-2xl font-semibold text-gray-900">Upload document</h1>
        <p class="mt-1 text-sm text-gray-500">Upload a PDF to generate AI-powered quizzes</p>
      </div>
      <div class="card"
           (dragover)="$event.preventDefault(); dragActive.set(true)"
           (dragleave)="dragActive.set(false)"
           (drop)="$event.preventDefault(); dragActive.set(false); onDrop($event)">
        <input #fileInput type="file" accept=".pdf,application/pdf" (change)="onFileChange($event)"
               class="sr-only" />
        <div (click)="fileInput.click()" role="button" tabindex="0"
             class="w-full border-2 border-dashed rounded-xl p-8 text-center transition-colors cursor-pointer focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2"
             [class.border-blue-400]="dragActive()"
             [class.bg-blue-100]="dragActive()"
             [class.border-gray-300]="!dragActive()">
          <span class="text-4xl text-gray-400 block" aria-hidden="true">&#128196;</span>
          <span class="mt-2 block text-sm font-medium text-gray-700">Drop your PDF here or click to browse</span>
          <span class="mt-1 block text-xs text-gray-500">Only PDF files are supported</span>
        </div>
      </div>
      @if (file()) {
        <div class="mt-4 flex items-center justify-between gap-2 p-3 rounded-lg bg-gray-50 border border-gray-200">
          <span class="text-sm font-medium text-gray-900 truncate">{{ file()?.name }}</span>
          <button type="button" (click)="file.set(null); errorMessage.set(null)" class="text-sm text-gray-500 hover:text-gray-700 shrink-0">Remove</button>
        </div>
      }
      <form (ngSubmit)="onSubmit($event)" class="mt-4">
        <button type="submit" [disabled]="!file() || uploading()" class="btn-primary w-full">
          {{ uploading() ? 'Uploading…' : 'Upload' }}
        </button>
      </form>
      @if (errorMessage()) {
        <p class="mt-3 text-sm text-red-600" role="alert">{{ errorMessage() }}</p>
      }
    </div>
  `
})
export class UploadDocumentComponent {
  private readonly api = inject(StudyPilotApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  file = signal<File | null>(null);
  uploading = signal(false);
  errorMessage = signal<string | null>(null);
  dragActive = signal(false);

  onFileChange(e: Event): void {
    const input = e.target as HTMLInputElement;
    const f = input.files?.[0];
    if (f?.name.toLowerCase().endsWith('.pdf')) {
      this.file.set(f);
      this.errorMessage.set(null);
    } else {
      this.file.set(null);
      this.errorMessage.set(f ? 'Please select a PDF file.' : null);
    }
  }

  onDrop(e: DragEvent): void {
    const f = e.dataTransfer?.files?.[0];
    if (f?.name.toLowerCase().endsWith('.pdf')) {
      this.file.set(f);
      this.errorMessage.set(null);
    } else if (f) {
      this.file.set(null);
      this.errorMessage.set('Please select a PDF file.');
    }
  }

  onSubmit(event?: Event): void {
    event?.preventDefault();
    const f = this.file();
    if (!f) return;
    this.errorMessage.set(null);
    this.uploading.set(true);
    const formData = new FormData();
    formData.append('file', f, f.name);
    this.api.uploadDocument(formData).subscribe({
      next: () => {
        this.uploading.set(false);
        this.toast.success('Document uploaded. Processing started.');
        this.router.navigateByUrl('/documents');
      },
      error: (err: unknown) => {
        this.uploading.set(false);
        const msg = err instanceof EnterpriseApiError && err.errors.length > 0
          ? err.errors.map(e => e.message).join(' ')
          : 'Upload failed.';
        this.toast.error(msg);
        this.errorMessage.set(msg);
      }
    });
  }
}

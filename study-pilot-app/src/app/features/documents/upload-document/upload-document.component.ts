import { Component, inject, ChangeDetectionStrategy, signal, viewChild, ElementRef, NgZone } from '@angular/core';
import { Router } from '@angular/router';
import { timeout, finalize, TimeoutError } from 'rxjs';
import { StudyPilotApiService } from '@core/services/study-pilot-api.service';
import { ToastService } from '@shared/services/toast.service';
import { EnterpriseApiError } from '@core/http/enterprise-api-error';
import { HttpErrorResponse } from '@angular/common/http';

function isPdf(file: File): boolean {
  const name = (file.name || '').toLowerCase();
  return name.endsWith('.pdf') || file.type === 'application/pdf';
}

const UPLOAD_TIMEOUT_MS = 120_000; // 2 minutes
const REDIRECT_DELAY_MS = 2500;

type UploadStatus = 'idle' | 'uploading' | 'success' | 'error';

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
}

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

      @switch (uploadStatus()) {
        @case ('success') {
          <div class="rounded-xl border-2 border-green-200 bg-green-50 p-5 text-green-800" role="status">
            <p class="font-medium">Document uploaded</p>
            <p class="mt-1 text-sm opacity-90">Processing has started. You can start a quiz once it's ready.</p>
            <p class="mt-2 text-sm opacity-80">Taking you to your documents…</p>
          </div>
        }
        @default {
          <div class="card"
               (dragover)="$event.preventDefault(); dragActive.set(true)"
               (dragleave)="dragActive.set(false)"
               (drop)="$event.preventDefault(); dragActive.set(false); onDrop($event)">
            <input #fileInputRef type="file" accept=".pdf,application/pdf" (change)="onFileChange($event)"
                   class="sr-only" />
            <div (click)="triggerFileInput()" role="button" tabindex="0"
                 class="w-full border-2 border-dashed rounded-xl p-8 text-center transition-colors cursor-pointer focus-visible:ring-2 focus-visible:ring-blue-500 focus-visible:ring-offset-2"
                 [class.border-blue-400]="dragActive()"
                 [class.bg-blue-100]="dragActive()"
                 [class.border-gray-300]="!dragActive()"
                 [class.pointer-events-none]="uploadStatus() === 'uploading'"
                 [class.opacity-70]="uploadStatus() === 'uploading'">
              <span class="text-4xl text-gray-400 block" aria-hidden="true">&#128196;</span>
              <span class="mt-2 block text-sm font-medium text-gray-700">Drop your PDF here or click to browse</span>
              <span class="mt-1 block text-xs text-gray-500">Only PDF files are supported (max 10 MB)</span>
            </div>
          </div>

          <div class="mt-4 flex items-center justify-between gap-2 p-3 rounded-lg border border-gray-200 min-h-[52px]"
               [class.bg-gray-50]="file()"
               [class.bg-white]="!file()">
            @if (file(); as selectedFile) {
              <span class="text-sm font-medium text-gray-900 truncate" title="{{ selectedFile.name }}">{{ selectedFile.name }}</span>
              <span class="text-xs text-gray-500 shrink-0">{{ formatFileSize(selectedFile.size) }}</span>
              @if (uploadStatus() !== 'uploading') {
                <button type="button" (click)="clearFile($event)" class="text-sm text-gray-500 hover:text-gray-700 shrink-0">Remove</button>
              }
            } @else {
              <span class="text-sm text-gray-500">No file chosen</span>
              <span class="text-xs text-gray-400">Click the area above to select a PDF</span>
            }
          </div>

          @if (uploadStatus() === 'uploading') {
            <div class="mt-4 rounded-xl border border-blue-200 bg-blue-50 p-4" role="status" aria-live="polite">
              <div class="flex items-center gap-3">
                <span class="upload-spinner h-5 w-5 shrink-0 rounded-full border-2 border-blue-300 border-t-blue-600" aria-hidden="true"></span>
                <div>
                  <p class="text-sm font-medium text-blue-800">Uploading your document…</p>
                  <p class="text-xs text-blue-700 mt-0.5">Please wait. Do not close this page.</p>
                </div>
              </div>
            </div>
          }

          <div class="mt-4">
            <button type="button"
                    (click)="startUpload()"
                    [disabled]="uploadStatus() === 'uploading' || !file()"
                    class="btn-primary w-full">
              @if (uploadStatus() === 'uploading') {
                Uploading…
              } @else if (file()) {
                Upload
              } @else {
                Select a PDF to upload
              }
            </button>
          </div>

          @if (uploadStatus() === 'error' && errorMessage(); as msg) {
            <div class="mt-3 rounded-lg border border-red-200 bg-red-50 p-3" role="alert">
              <p class="text-sm font-medium text-red-800">{{ msg }}</p>
              <p class="mt-1 text-xs text-red-600">You can try again or choose another file.</p>
            </div>
          }
        }
      }
    </div>
  `,
  styles: [`
    .upload-spinner {
      animation: upload-spin 0.8s linear infinite;
    }
    @keyframes upload-spin {
      to { transform: rotate(360deg); }
    }
  `]
})
export class UploadDocumentComponent {
  private readonly api = inject(StudyPilotApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly ngZone = inject(NgZone);
  fileInputRef = viewChild<ElementRef<HTMLInputElement>>('fileInputRef');
  file = signal<File | null>(null);
  /** 'idle' | 'uploading' | 'success' | 'error' */
  uploadStatus = signal<UploadStatus>('idle');
  errorMessage = signal<string | null>(null);
  dragActive = signal(false);
  protected readonly formatFileSize = formatFileSize;

  triggerFileInput(): void {
    this.fileInputRef()?.nativeElement?.click();
  }

  clearFile(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.file.set(null);
    this.errorMessage.set(null);
    const input = this.fileInputRef()?.nativeElement;
    if (input) input.value = '';
  }

  onFileChange(e: Event): void {
    const input = e.target as HTMLInputElement;
    const f = input.files?.[0];
    if (f && isPdf(f)) {
      this.file.set(f);
      this.errorMessage.set(null);
    } else {
      this.file.set(null);
      this.errorMessage.set(f ? 'Please select a PDF file.' : null);
      if (input) input.value = '';
    }
  }

  onDrop(e: DragEvent): void {
    e.preventDefault();
    const f = e.dataTransfer?.files?.[0];
    if (f && isPdf(f)) {
      this.file.set(f);
      this.errorMessage.set(null);
    } else if (f) {
      this.file.set(null);
      this.errorMessage.set('Please select a PDF file.');
    }
  }

  private getErrorMessage(err: unknown): string {
    if (err instanceof TimeoutError) {
      return 'Upload is taking too long. Please check your connection and try again.';
    }
    if (err instanceof EnterpriseApiError && err.errors.length > 0) {
      return err.errors.map(e => e.message).join(' ');
    }
    if (err instanceof HttpErrorResponse) {
      const body = err.error;
      if (body && typeof body === 'object' && Array.isArray((body as { errors?: unknown }).errors)) {
        const first = (body as { errors: { message?: string }[] }).errors[0];
        if (first?.message) return first.message;
      }
      if (err.status === 0) return 'Cannot reach the server. Check your connection and that the app is running.';
      if (err.status === 401) return 'Session expired. Please sign in again.';
      if (err.status === 413) return 'File is too large. Maximum size is 10 MB.';
      if (err.status === 429) return 'Too many uploads. Please wait a moment and try again.';
      return err.message || `Upload failed (${err.status}). Please try again.`;
    }
    if (err instanceof Error) return err.message;
    return 'Upload failed. Please try again.';
  }

  /** Called by button (click). No form submit — prevents page reload and ensures XHR is sent. */
  startUpload(): void {
    const f = this.file();
    if (!f) {
      this.errorMessage.set('Please select a PDF first. Click the area above to browse or drag and drop a file.');
      this.toast.error('Select a PDF file to upload.');
      this.triggerFileInput();
      return;
    }
    this.errorMessage.set(null);
    this.uploadStatus.set('uploading');
    const formData = new FormData();
    formData.append('file', f, f.name);
    this.api.uploadDocument(formData).pipe(
      timeout(UPLOAD_TIMEOUT_MS),
      finalize(() => this.ngZone.run(() => {
        if (this.uploadStatus() === 'uploading') this.uploadStatus.set('idle');
      }))
    ).subscribe({
      next: () => {
        this.ngZone.run(() => {
          this.uploadStatus.set('success');
          this.toast.success('Document uploaded. Processing started.');
          setTimeout(() => this.router.navigateByUrl('/documents'), REDIRECT_DELAY_MS);
        });
      },
      error: (err: unknown) => {
        this.ngZone.run(() => {
          this.uploadStatus.set('error');
          const msg = this.getErrorMessage(err);
          this.toast.error(msg);
          this.errorMessage.set(msg);
        });
      }
    });
  }
}

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
    <div class="p-4 max-w-md">
      <h1 class="text-xl font-semibold mb-4">Upload document</h1>
      <form (ngSubmit)="onSubmit($event)">
        <input type="file" accept=".pdf,application/pdf" (change)="onFileChange($event)" class="mb-4 block w-full text-sm" />
        <button type="submit" [disabled]="!file() || uploading()"
                class="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 disabled:opacity-50">
          {{ uploading() ? 'Uploading...' : 'Upload' }}
        </button>
      </form>
      @if (file()) {
        <p class="mt-2 text-sm text-gray-600">Selected: {{ file()?.name }}</p>
      }
      @if (errorMessage()) {
        <p class="mt-2 text-sm text-red-600">{{ errorMessage() }}</p>
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

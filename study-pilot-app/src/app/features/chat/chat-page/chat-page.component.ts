import {
  Component,
  inject,
  ChangeDetectionStrategy,
  signal,
  OnInit,
  ViewChild,
  ElementRef,
  afterNextRender,
  DestroyRef,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  StudyPilotApiService,
  ChatMessageItem,
  GetChatHistoryResponse,
  DocumentItem,
} from '@core/services/study-pilot-api.service';

@Component({
  selector: 'app-chat-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-page.component.html',
})
export class ChatPageComponent implements OnInit {
  private readonly api = inject(StudyPilotApiService);
  private readonly destroyRef = inject(DestroyRef);

  sessionId = signal<string | null>(null);
  documentId = signal<string | null>(null);
  documents = signal<DocumentItem[]>([]);
  messages = signal<ChatMessageItem[]>([]);
  totalCount = signal(0);
  pageSize = 50;
  loading = signal(false);
  sending = signal(false);
  error = signal<string | null>(null);
  inputText = signal('');
  selectedDocumentId = signal<string | null>(null);

  @ViewChild('messagesEnd') messagesEndRef?: ElementRef<HTMLDivElement>;

  constructor() {
    afterNextRender(() => {
      this.scrollToBottom();
    });
  }

  ngOnInit(): void {
    this.api
      .getDocuments()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: list => this.documents.set(list),
        error: () => this.documents.set([]),
      });
    this.startOrResumeSession();
  }

  startOrResumeSession(): void {
    const docId = this.selectedDocumentId();
    this.loading.set(true);
    this.error.set(null);
    this.api
      .createChatSession(docId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          this.sessionId.set(res.sessionId);
          this.documentId.set(docId ?? null);
          this.loadHistory(res.sessionId);
        },
        error: err => {
          this.loading.set(false);
          this.error.set(err?.message ?? 'Failed to start chat session.');
        },
      });
  }

  loadHistory(sid: string): void {
    this.api
      .getChatHistory(sid, 1, this.pageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res: GetChatHistoryResponse) => {
          this.messages.set(res.messages ?? []);
          this.totalCount.set(res.totalCount ?? 0);
          this.loading.set(false);
          this.scrollToBottom();
        },
        error: () => {
          this.loading.set(false);
          this.messages.set([]);
        },
      });
  }

  send(): void {
    const sid = this.sessionId();
    const content = this.inputText().trim();
    if (!sid || !content) return;
    this.sending.set(true);
    this.error.set(null);
    this.inputText.set('');
    this.api
      .sendChatMessage(sid, content)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.sending.set(false);
          this.loadHistory(sid);
        },
        error: err => {
          this.sending.set(false);
          this.inputText.set(content);
          this.error.set(err?.message ?? 'Failed to send message.');
        },
      });
  }

  switchToGlobal(): void {
    this.selectedDocumentId.set(null);
    this.startOrResumeSession();
  }

  switchToDocument(docId: string): void {
    this.selectedDocumentId.set(docId);
    this.startOrResumeSession();
  }

  private scrollToBottom(): void {
    setTimeout(() => this.messagesEndRef?.nativeElement?.scrollIntoView({ behavior: 'smooth' }), 100);
  }
}

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
import { RouterLink } from '@angular/router';
import {
  StudyPilotApiService,
  ChatMessageItem,
  GetChatHistoryResponse,
  DocumentItem,
} from '@core/services/study-pilot-api.service';
import { ChatStreamService, StreamMessage } from './chat-stream.service';

@Component({
  selector: 'app-chat-stream',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './chat-stream.component.html',
})
export class ChatStreamComponent implements OnInit {
  private readonly api = inject(StudyPilotApiService);
  private readonly streamService = inject(ChatStreamService);
  private readonly destroyRef = inject(DestroyRef);

  sessionId = signal<string | null>(null);
  documentId = signal<string | null>(null);
  documents = signal<DocumentItem[]>([]);
  messages = signal<ChatMessageItem[]>([]);
  totalCount = signal(0);
  pageSize = 50;
  loading = signal(false);
  sending = signal(false);
  streaming = signal(false);
  error = signal<string | null>(null);
  inputText = signal('');
  selectedDocumentId = signal<string | null>(null);
  /** Accumulated streamed text for the current assistant reply (shown live). */
  streamingContent = signal('');

  @ViewChild('messagesEnd') messagesEndRef?: ElementRef<HTMLDivElement>;

  private abortController: AbortController | null = null;

  constructor() {
    afterNextRender(() => this.scrollToBottom());
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
          this.streamingContent.set('');
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

    this.abortController?.abort();
    this.abortController = new AbortController();

    this.sending.set(true);
    this.streaming.set(true);
    this.error.set(null);
    this.inputText.set('');
    this.streamingContent.set('');

    this.streamService
      .stream(sid, content, this.abortController.signal)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (msg: StreamMessage) => {
          if (msg.type === 'token' && msg.data) this.streamingContent.update(c => c + msg.data);
          else if (msg.type === 'done') {
            this.streaming.set(false);
            this.sending.set(false);
            this.loadHistory(sid);
          }
          this.scrollToBottom();
        },
        error: err => {
          this.streaming.set(false);
          this.sending.set(false);
          this.inputText.set(content);
          this.error.set(err?.message ?? 'Stream failed.');
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

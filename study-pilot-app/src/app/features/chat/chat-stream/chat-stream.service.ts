import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService } from '@core/auth/auth.service';
import { APP_ENVIRONMENT } from '@core/config/environment.token';

export interface StreamMessage {
  type: 'token' | 'done';
  data?: string;
}

@Injectable({ providedIn: 'root' })
export class ChatStreamService {
  private readonly auth = inject(AuthService);
  private readonly env = inject(APP_ENVIRONMENT);

  /**
   * Stream chat response via GET /chat/stream (SSE).
   * Uses fetch() so we can set Authorization header (EventSource does not support custom headers).
   * Abort previous stream when a new message is sent by passing the same AbortSignal.
   */
  stream(sessionId: string, message: string, signal?: AbortSignal): Observable<StreamMessage> {
    return new Observable<StreamMessage>(subscriber => {
      const token = this.auth.token;
      if (!token) {
        subscriber.error(new Error('Not authenticated'));
        return;
      }
      const baseUrl = this.env.apiBaseUrl.replace(/\/$/, '');
      const url = `${baseUrl}/chat/stream?sessionId=${encodeURIComponent(sessionId)}&message=${encodeURIComponent(message)}`;
      const controller = signal ? undefined : new AbortController();
      const abortSignal = signal ?? controller?.signal;

      fetch(url, {
        method: 'GET',
        headers: { Authorization: `Bearer ${token}` },
        signal: abortSignal,
      })
        .then(async res => {
          if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err?.errors?.[0]?.message ?? `Stream failed: ${res.status}`);
          }
          const reader = res.body?.getReader();
          if (!reader) {
            subscriber.complete();
            return;
          }
          const decoder = new TextDecoder();
          let buffer = '';
          try {
            while (true) {
              const { done, value } = await reader.read();
              if (done) break;
              buffer += decoder.decode(value, { stream: true });
              const lines = buffer.split('\n');
              buffer = lines.pop() ?? '';
              let eventType = '';
              for (const line of lines) {
                if (line.startsWith('event: ')) eventType = line.slice(7).trim();
                else if (line.startsWith('data: ') && eventType) {
                  let data = line.slice(6).replace(/\\n/g, '\n').replace(/\\r/g, '\r');
                  if (eventType === 'token') subscriber.next({ type: 'token', data });
                  else if (eventType === 'done') subscriber.next({ type: 'done' });
                  eventType = '';
                }
              }
            }
          } finally {
            reader.releaseLock();
          }
          subscriber.complete();
        })
        .catch(err => {
          if (err?.name === 'AbortError') subscriber.complete();
          else subscriber.error(err);
        });

      return () => controller?.abort();
    });
  }
}

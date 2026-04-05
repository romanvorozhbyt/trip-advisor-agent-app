import { Injectable, NgZone, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ChatRequest } from '../models/chat-request.model';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly apiUrl = `${environment.apiUrl}/chat`;
  private readonly auth = inject(AuthService);

  constructor(private zone: NgZone) {}

  sendMessageStream(request: ChatRequest): Observable<{ type: 'metadata' | 'content' | 'done'; data: string }> {
    return new Observable((observer) => {
      const abortController = new AbortController();

      fetch(`${this.apiUrl}/stream`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(this.auth.getToken() ? { 'Authorization': `Bearer ${this.auth.getToken()}` } : {}),
        },
        body: JSON.stringify(request),
        signal: abortController.signal,
      })
        .then((response) => {
          if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
          }

          const reader = response.body?.getReader();
          if (!reader) {
            throw new Error('No response body');
          }

          const decoder = new TextDecoder();
          let buffer = '';

          const read = (): void => {
            reader
              .read()
              .then(({ done, value }) => {
                if (done) {
                  this.zone.run(() => observer.complete());
                  return;
                }

                buffer += decoder.decode(value, { stream: true });
                const lines = buffer.split('\n');
                buffer = lines.pop() ?? '';

                for (let i = 0; i < lines.length; i++) {
                  const line = lines[i];
                  if (line.startsWith('data: ')) {
                    const data = line.slice(6);

                    if (data === '[DONE]') {
                      this.zone.run(() => {
                        observer.next({ type: 'done', data: '' });
                        observer.complete();
                      });
                      return;
                    }

                    const prevLine = i > 0 ? lines[i - 1] : '';

                    if (prevLine === 'event: metadata') {
                      try {
                        const parsed = JSON.parse(data);
                        this.zone.run(() =>
                          observer.next({ type: 'metadata', data: parsed.conversationId })
                        );
                      } catch {
                        // ignore parse errors
                      }
                    } else {
                      this.zone.run(() => observer.next({ type: 'content', data }));
                    }
                  }
                }

                read();
              })
              .catch((err) => {
                this.zone.run(() => observer.error(err));
              });
          };

          read();
        })
        .catch((err) => {
          if (err.name !== 'AbortError') {
            this.zone.run(() => observer.error(err));
          }
        });

      return () => abortController.abort();
    });
  }

  deleteConversation(conversationId: string): Observable<void> {
    return new Observable((observer) => {
      fetch(`${this.apiUrl}/${encodeURIComponent(conversationId)}`, {
        method: 'DELETE',
        headers: {
          ...(this.auth.getToken() ? { 'Authorization': `Bearer ${this.auth.getToken()}` } : {}),
        },
      })
        .then(() => {
          this.zone.run(() => {
            observer.next();
            observer.complete();
          });
        })
        .catch((err) => this.zone.run(() => observer.error(err)));
    });
  }
}

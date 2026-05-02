import { Injectable, signal } from '@angular/core';
import { Conversation } from '../models/conversation.model';
import { ChatMessage } from '../models/chat-message.model';

const STORAGE_KEY = 'trip-advisor-conversations';

@Injectable({ providedIn: 'root' })
export class ConversationStorageService {
  readonly conversations = signal<Conversation[]>(this.loadAll());

  loadAll(): Conversation[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw) as Conversation[];
      return parsed.sort((a, b) => b.updatedAt - a.updatedAt);
    } catch {
      return [];
    }
  }

  get(id: string): Conversation | undefined {
    return this.conversations().find((c) => c.id === id);
  }

  save(id: string, messages: ChatMessage[]): void {
    const now = Date.now();
    const existing = this.get(id);
    const title = this.deriveTitle(messages) || existing?.title || 'New Chat';

    const conversation: Conversation = {
      id,
      title,
      messages: messages.map(({ role, content }) => ({ role, content })),
      createdAt: existing?.createdAt ?? now,
      updatedAt: now,
    };

    this.conversations.update((list) => {
      const filtered = list.filter((c) => c.id !== id);
      return [conversation, ...filtered];
    });
    this.persist();
  }

  delete(id: string): void {
    this.conversations.update((list) => list.filter((c) => c.id !== id));
    this.persist();
  }

  private persist(): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(this.conversations()));
  }

  private deriveTitle(messages: ChatMessage[]): string {
    const first = messages.find((m) => m.role === 'user');
    if (!first) return 'New Chat';
    const text = first.content.trim();
    return text.length > 50 ? text.slice(0, 50) + '…' : text;
  }
}

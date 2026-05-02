import {
  Component,
  ChangeDetectionStrategy,
  signal,
  inject,
  ViewChild,
  ElementRef,
  AfterViewChecked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../../services/chat.service';
import { ConversationStorageService } from '../../services/conversation-storage.service';
import { AuthService } from '../../services/auth.service';
import { ChatMessage } from '../../models/chat-message.model';
import { Conversation } from '../../models/conversation.model';
import { ChatMessageComponent } from '../../components/chat-message/chat-message.component';
import { ChatHistoryComponent } from '../../components/chat-history/chat-history.component';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-chat',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ChatMessageComponent, ChatHistoryComponent],
  templateUrl: './chat.component.html',
  styleUrl: './chat.component.scss',
})
export class ChatComponent implements AfterViewChecked {
  @ViewChild('messagesContainer') private messagesContainer!: ElementRef<HTMLDivElement>;

  private readonly chatService = inject(ChatService);
  readonly storage = inject(ConversationStorageService);
  readonly auth = inject(AuthService);

  readonly messages = signal<ChatMessage[]>([]);
  readonly userInput = signal('');
  readonly isLoading = signal(false);
  readonly sidebarOpen = signal(false);

  conversationId: string | undefined;
  private streamSub: Subscription | null = null;
  private shouldScroll = false;

  ngAfterViewChecked(): void {
    if (this.shouldScroll) {
      this.scrollToBottom();
      this.shouldScroll = false;
    }
  }

  send(): void {
    const text = this.userInput().trim();
    if (!text || this.isLoading()) return;

    this.messages.update((msgs) => [...msgs, { role: 'user', content: text }]);
    this.userInput.set('');
    this.isLoading.set(true);
    this.shouldScroll = true;

    this.messages.update((msgs) => [...msgs, { role: 'assistant', content: '', isStreaming: true }]);

    this.streamSub = this.chatService
      .sendMessageStream({ message: text, conversationId: this.conversationId })
      .subscribe({
        next: (event) => {
          if (event.type === 'metadata') {
            this.conversationId = event.data;
          } else if (event.type === 'content') {
            this.messages.update((msgs) => {
              const updated = [...msgs];
              const last = updated[updated.length - 1];
              updated[updated.length - 1] = { ...last, content: last.content + event.data };
              return updated;
            });
            this.shouldScroll = true;
          } else if (event.type === 'done') {
            this.messages.update((msgs) => {
              const updated = [...msgs];
              updated[updated.length - 1] = { ...updated[updated.length - 1], isStreaming: false };
              return updated;
            });
            this.isLoading.set(false);
            this.persistCurrentChat();
          }
        },
        error: () => {
          this.messages.update((msgs) => {
            const updated = [...msgs];
            const last = updated[updated.length - 1];
            updated[updated.length - 1] = {
              ...last,
              content: last.content + '\n\n⚠ Connection error. Please try again.',
              isStreaming: false,
            };
            return updated;
          });
          this.isLoading.set(false);
          this.persistCurrentChat();
        },
        complete: () => {
          this.messages.update((msgs) => {
            if (!msgs.length) return msgs;
            const updated = [...msgs];
            updated[updated.length - 1] = { ...updated[updated.length - 1], isStreaming: false };
            return updated;
          });
          this.isLoading.set(false);
        },
      });
  }

  sendSuggestion(text: string): void {
    this.userInput.set(text);
    this.send();
  }

  newConversation(): void {
    this.streamSub?.unsubscribe();
    this.messages.set([]);
    this.conversationId = undefined;
    this.isLoading.set(false);
  }

  loadConversation(conv: Conversation): void {
    this.streamSub?.unsubscribe();
    this.conversationId = conv.id;
    this.messages.set(conv.messages);
    this.isLoading.set(false);
    this.sidebarOpen.set(false);
    this.shouldScroll = true;
  }

  deleteConversation(id: string): void {
    this.chatService.deleteConversation(id).subscribe();
    this.storage.delete(id);
    if (this.conversationId === id) {
      this.newConversation();
    }
  }

  toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  private persistCurrentChat(): void {
    if (this.conversationId && this.messages().length) {
      this.storage.save(this.conversationId, this.messages());
    }
  }

  private scrollToBottom(): void {
    const el = this.messagesContainer?.nativeElement;
    if (el) {
      el.scrollTop = el.scrollHeight;
    }
  }
}

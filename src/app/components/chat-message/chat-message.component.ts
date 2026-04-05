import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';
import { ChatMessage } from '../../models/chat-message.model';

marked.setOptions({
  breaks: true,
  gfm: true,
});

@Component({
  selector: 'app-chat-message',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'class': 'message',
    '[class.user]': 'message().role === "user"',
    '[class.assistant]': 'message().role === "assistant"',
    'role': 'listitem',
  },
  templateUrl: './chat-message.component.html',
  styleUrl: './chat-message.component.scss',
})
export class ChatMessageComponent {
  message = input.required<ChatMessage>();

  renderedContent = computed<SafeHtml>(() => {
    const msg = this.message();
    if (msg.role === 'user') {
      return msg.content;
    }
    const html = marked.parse(msg.content, { async: false }) as string;
    return this.sanitizer.bypassSecurityTrustHtml(html);
  });

  constructor(private sanitizer: DomSanitizer) {}
}

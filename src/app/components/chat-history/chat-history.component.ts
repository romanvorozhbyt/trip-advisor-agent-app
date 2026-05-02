import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Conversation } from '../../models/conversation.model';

@Component({
  selector: 'app-chat-history',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  host: {
    'class': 'chat-history',
    'role': 'navigation',
    '[attr.aria-label]': '"Chat history"',
  },
  templateUrl: './chat-history.component.html',
  styleUrl: './chat-history.component.scss',
})
export class ChatHistoryComponent {
  conversations = input.required<Conversation[]>();
  activeId = input<string>();

  selected = output<Conversation>();
  deleted = output<string>();
  closed = output<void>();

  onDelete(event: Event, id: string): void {
    event.stopPropagation();
    this.deleted.emit(id);
  }
}

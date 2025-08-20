// File: src/app/components/code-prompt/code-prompt.component.ts
import { Component, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CodeContextService } from '../../services/code-context.service';

interface ConversationMeta {
  id: string;
  name: string;
  project?: string;
  summary?: string;
}

@Component({
  standalone: true,
  selector: 'bt-code-prompt',
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex flex-col gap-4">
      <textarea [(ngModel)]="prompt" placeholder="Ask a question about the project..." rows="4" class="border p-2 rounded"></textarea>
      <button (click)="submitPrompt()" class="bg-green-600 text-white px-4 py-1 rounded" [disabled]="isStreaming">Submit</button>
      <div *ngIf="isStreaming" class="italic text-sm text-gray-500">Streaming response...</div>
      <div #scrollTarget class="overflow-auto bg-gray-100 p-4 rounded h-64 whitespace-pre-wrap border" style="max-height: 400px;">
        {{ response }}
      </div>
      <div class="flex items-center gap-2 mt-4">
        <label for="conversationSelect">Conversation:</label>
        <select [(ngModel)]="conversationId" (change)="selectConversation()" class="border px-2 py-1 rounded w-full">
          <option *ngFor="let convo of conversations" [value]="convo.id">{{ convo.name }}</option>
        </select>
        <button (click)="renameConversation()" class="text-xs underline text-blue-600">Rename</button>
      </div>
    </div>
  `
})
export class CodePromptComponent implements AfterViewInit {
  prompt: string = '';
  response: string = '';
  isStreaming = false;
  conversationId: string;
  conversations: ConversationMeta[] = [];

  @ViewChild('scrollTarget') scrollTarget!: ElementRef;

  constructor(private contextService: CodeContextService) {
    this.conversations = this.loadConversations();
    this.conversationId = this.contextService.getConversationId();
    if (!this.conversations.some(c => c.id === this.conversationId)) {
      const defaultName = this.generateTitleFromId(this.conversationId);
      const summary = this.prompt.slice(0, 80) + (this.prompt.length > 80 ? '...' : '');
      const project = this.contextService.getCurrentFolder() || 'Unnamed Project';
      this.conversations.push({ id: this.conversationId, name: defaultName, project, summary });
      this.saveConversations();
    }
  }

  ngAfterViewInit() {
    this.scrollToBottom();
  }

  submitPrompt() {
    if (!this.prompt.trim()) return;
    this.response = '';
    this.isStreaming = true;

    const eventSource = new EventSource(`/ask/code/stream?conversationId=${this.conversationId}&prompt=${encodeURIComponent(this.prompt.trim())}`);

    eventSource.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        if (data.done) {
          this.isStreaming = false;
          eventSource.close();
        } else {
          this.response += data.response || '';
          this.scrollToBottom();
        }
      } catch (err) {
        console.error('Stream error:', err);
        this.response += '\n[Stream error]';
        this.isStreaming = false;
        eventSource.close();
      }
    };

    eventSource.onerror = () => {
      this.response += '\n[Error receiving response]';
      this.isStreaming = false;
      eventSource.close();
    };
  }

  scrollToBottom() {
    if (this.scrollTarget) {
      setTimeout(() => {
        this.scrollTarget.nativeElement.scrollTop = this.scrollTarget.nativeElement.scrollHeight;
      }, 0);
    }
  }

  selectConversation() {
    localStorage.setItem('codeConversationId', this.conversationId);
  }

  renameConversation() {
    const convo = this.conversations.find(c => c.id === this.conversationId);
    if (!convo) return;
    const newName = prompt('Enter a new name for this conversation:', convo.name);
    if (newName && newName.trim()) {
      convo.name = newName.trim();
      this.saveConversations();
    }
  }

  generateTitleFromId(id: string): string {
    return 'Conversation ' + id.slice(0, 8);
  }

  loadConversations(): ConversationMeta[] {
    try {
      const raw = localStorage.getItem('conversationList');
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }

  saveConversations() {
    localStorage.setItem('conversationList', JSON.stringify(this.conversations));
  }
}

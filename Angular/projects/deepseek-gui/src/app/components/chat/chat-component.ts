import { Component, NgZone, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OllamaService } from '../../ollama.service';
import { generateGUID } from '../code-gen/code-gen.component';
import { ThinkingProgressComponent } from '../thinking-progress/thinking-progress.component';
import { Clipboard } from '@angular/cdk/clipboard';
import { MatIconModule } from '@angular/material/icon';

interface Message {
  isUser: boolean;
  text: string;
  thinkContent?: string;
  showThinking: boolean;
}

@Component({
  selector: 'app-chat',
  templateUrl: './chat-component.html',
  styleUrls: ['./chat-component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ThinkingProgressComponent,
    MatIconModule
  ]
})
export class ChatComponent {
  @ViewChild('chatContainer') chatContainer?: ElementRef<HTMLDivElement>;

  messages: Message[] = [];
  prompt = '';
  error = '';
  conversationId: string = generateGUID();
  inProgress = false;

  constructor(
    private ollamaService: OllamaService,
    private _clipboard: Clipboard,
    private ngZone: NgZone
  ) {}

  sendMessage(event: Event, msg: string): void {
    event.preventDefault();

    const trimmed = this.prompt.trim();
    if (!trimmed) {
      this.error = 'Please ask a question first.';
      return;
    }

    this.error = '';
    this.prompt = '';
    this.messages.push({ isUser: true, text: trimmed, showThinking: false });
    this.scrollToBottom();

    const assistantMsg: Message = {
      isUser: false,
      text: '',
      showThinking: false
    };
    this.messages.push(assistantMsg);
    this.scrollToBottom();

    let aggregatedText = '';
    let capturedThink = '';
    let insideThink = false;

    this.inProgress = true;

    this.ollamaService.sendPrompt(this.conversationId, trimmed, 'chat')
      .subscribe({
        next: (chunk) => {
          this.ngZone.run(() => {
            const response = chunk.response ?? '';

            // Track inside <think> block due to fragmented chunks
            let temp = response;

            if (temp.includes('<think>')) {
              insideThink = true;
              temp = temp.replace('<think>', '');
            }
            if (temp.includes('</think>')) {
              insideThink = false;
              temp = temp.replace('</think>', '');
            }

            if (insideThink) {
              capturedThink += temp;
            } else {
              aggregatedText += temp;
            }

            assistantMsg.text = aggregatedText.trim();
            assistantMsg.thinkContent = capturedThink.trim() || undefined;
            assistantMsg.showThinking = false;

            this.scrollToBottom();
          });
        },
        error: (err: unknown) => {
          this.ngZone.run(() => {
            assistantMsg.text = '⚠️ Error: Unable to retrieve response.';
            assistantMsg.showThinking = false;
            this.error = err instanceof Error
              ? `An error occurred: ${err.message}`
              : 'An unknown error occurred.';
            this.inProgress = false;
            this.scrollToBottom();
          });
        },
        complete: () => {
          this.ngZone.run(() => {
            assistantMsg.text = aggregatedText.trim();
            assistantMsg.thinkContent = capturedThink.trim() || undefined;
            assistantMsg.showThinking = false;
            this.inProgress = false;
            this.scrollToBottom();
          });
        }
      });
  }

  scrollToBottom(): void {
    setTimeout(() => {
      if (this.chatContainer?.nativeElement) {
        const el = this.chatContainer.nativeElement;
        el.scrollTop = el.scrollHeight;
      }
    }, 100);
  }

  copyToClipboard(message: Message): void {
    const value = message.showThinking && message.thinkContent
      ? message.thinkContent
      : message.text;
    this._clipboard.copy(value);
  }
}

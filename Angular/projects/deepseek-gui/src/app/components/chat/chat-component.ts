// chat.component.ts (improved message flow)

import { Component, NgZone } from '@angular/core';
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
  messages: Message[] = [];
  prompt = '';
  error = '';
  conversationId: string = generateGUID();

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

    // Add placeholder assistant message
    const assistantMsg: Message = {
      isUser: false,
      text: '',
      showThinking: true
    };
    this.messages.push(assistantMsg);

    let aggregatedText = '';
    let capturedThink: string | undefined;

    this.ollamaService.sendPrompt(this.conversationId, trimmed, 'chat')
      .subscribe({
        next: (chunk: string) => {
          this.ngZone.run(() => {
            if (!chunk.startsWith('{')) return; // skip invalid or header lines
            try {
              const parsed = JSON.parse(chunk);
              const response = parsed.response ?? '';

              const thinkMatch = response.match(/<think>([\s\S]*?)<\/think>/);
              const cleanChunk = thinkMatch ? response.replace(thinkMatch[0], '') : response;

              if (thinkMatch && !capturedThink) {
                capturedThink = thinkMatch[1].trim();
              }

              aggregatedText += cleanChunk;
            } catch (err) {
              console.warn('⚠️ Failed to parse chunk:', chunk, err);
            }
          });
        },
        error: (err: unknown) => {
          this.ngZone.run(() => {
            assistantMsg.text = '⚠️ Error: Unable to retrieve response.';
            assistantMsg.showThinking = false;
            this.error = err instanceof Error
              ? `An error occurred: ${err.message}`
              : 'An unknown error occurred.';
          });
        },
        complete: () => {
          this.ngZone.run(() => {
            assistantMsg.text = aggregatedText;
            assistantMsg.thinkContent = capturedThink;
            assistantMsg.showThinking = false;
          });
        }
      });
  }

  copyToClipboard(message: Message): void {
    const value = message.showThinking && message.thinkContent
      ? message.thinkContent
      : message.text;
    this._clipboard.copy(value);
  }
}

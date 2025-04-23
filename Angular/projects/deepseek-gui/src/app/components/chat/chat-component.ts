import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OllamaService } from '../../ollama.service';
import { generateGUID } from '../code-gen/code-gen.component';
import { BehaviorSubject } from 'rxjs';
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
  standalone: true, // <-- Use standalone components for Angular 14+
  imports:[CommonModule, FormsModule, ThinkingProgressComponent, MatIconModule]
})
export class ChatComponent {
  messages: Message[] = [];
  prompt = '';
  error: string = '';
  conversationId: string = generateGUID();
  
  thinking$ = new BehaviorSubject<boolean>(false);

  constructor(private ollamaService: OllamaService, private _clipboard: Clipboard) {}

  sendMessage(event: Event, msg: string): void {
    event.preventDefault();
    
    if (!this.prompt.trim()) {
      this.error = 'Please ask a question first.';
      return;
    }
  
    this.thinking$.next(true); 
    this.error = '';
  
    this.messages.push({ isUser: true, text: this.prompt, showThinking: false });
  
    this.ollamaService.sendPrompt(this.conversationId, this.prompt, 'chat')
      .subscribe({
        next: (chunk: string) => { 
          const thinkMatch = chunk.match(/<think>([\s\S]*?)<\/think>/);
          if (thinkMatch) {
            this.messages.push({
              isUser: false,
              text: chunk.replace(/<think>[\s\S]*?<\/think>/, '').trim(), 
              thinkContent: thinkMatch[1].trim(),  
              showThinking: false
            });
          } else {
            this.messages.push({ isUser: false, text: chunk, showThinking: false });
          }
        },
        error: (err) => {
          console.error('Error:', err);
          this.error = 'An error occurred while processing your request.';
          this.thinking$.next(false); 
        },
        complete: () => {
          this.thinking$.next(false); 
          this.prompt = '';
        }
      });
  }

  copyToClipboard(message: Message): void {
    if (message.isUser) {
      this._clipboard.copy(message.text);
    } else {
      if (message.showThinking && message.thinkContent) {
        this._clipboard.copy(message.thinkContent);
      } else {
        this._clipboard.copy(message.text);
      }
    }
  }
}
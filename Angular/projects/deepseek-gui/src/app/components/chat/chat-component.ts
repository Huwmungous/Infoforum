import { Component } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OllamaService } from '../../ollama.service';
import { generateGUID } from '../code-gen/code-gen.component';

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
  imports:[CommonModule, FormsModule]
})

export class ChatComponent {
  messages: Message[] = [];
  prompt = '';
  loading: boolean = false;
  error: string = '';
  
  conversationId: string = generateGUID();

  constructor(private ollamaService: OllamaService ) {}
  

  sendMessage(event: Event, msg: string): void {
    event.preventDefault();
    if (!this.prompt.trim()) {
      this.error = 'Please ask a question first.';
      return;
    }
  
    this.loading = true;
    this.error = '';
  
    this.messages.push({ isUser: true, text: this.prompt, showThinking: false });
  
    this.ollamaService.sendPrompt(this.conversationId, this.prompt, 'chat')
      .subscribe({
        next: (chunk: string) => {
          // Parse for <think> and extract if present
          const thinkMatch = chunk.match(/<think>(.*?)<\/think>/);
          if (thinkMatch) {
            this.messages.push({
              isUser: false,
              text: chunk.replace(/<think>(.*?)<\/think>/, '').trim(), 
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
          this.loading = false;
        },
        complete: () => {
          this.loading = false;
          this.prompt = '';
        }
      });
    this.prompt = '';
  }

}
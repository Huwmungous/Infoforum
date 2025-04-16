import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BehaviorSubject } from 'rxjs';
import { Clipboard } from '@angular/cdk/clipboard';
import { MatIconModule } from '@angular/material/icon';
import { ThinkingProgressComponent } from '../thinking-progress/thinking-progress.component';
import { OllamaService } from '../../ollama.service';
import { generateGUID } from '../code-gen/code-gen.component';

interface ImageMessage {
  isUser: boolean;
  text: string;
  imageData?: string; // Base64 string that can be used as image src
}

@Component({
  selector: 'app-images',
  templateUrl: './image-gen.component.html',
  styleUrls: ['./image-gen.component.scss'],
  standalone: true,
  imports: [CommonModule, FormsModule, ThinkingProgressComponent, MatIconModule]
})
export class ImageGenComponent {
  messages: ImageMessage[] = [];
  prompt = '';
  error = '';
  conversationId: string = generateGUID();
  
  // Manages the "thinking" progress indicator
  thinking$ = new BehaviorSubject<boolean>(false);

  constructor(private ollamaService: OllamaService, private _clipboard: Clipboard) {}

  sendImage(event: Event, prompt: string): void {
    event.preventDefault();

    if (!prompt.trim()) {
      this.error = 'Please enter a prompt for image generation.';
      return;
    }

    this.thinking$.next(true);
    this.error = '';
    
    // Add the user message to the list
    this.messages.push({ isUser: true, text: prompt });
    
    // Call the service with dest "image" (the conversationId may not be needed here so we use an empty string)
    this.ollamaService.sendPrompt(this.conversationId, prompt, 'image')
      .subscribe({
        next: (blob: Blob) => {
          // Convert the Blob to a base64 data URL to be used as an <img> src
          const reader = new FileReader();
          reader.onload = () => {
            this.messages.push({ isUser: false, text: '', imageData: reader.result as string });
          };
          reader.readAsDataURL(blob);
        },
        error: (err) => {
          console.error('Error generating image:', err);
          this.error = 'An error occurred while generating the image.';
          this.thinking$.next(false);
        },
        complete: () => {
          this.thinking$.next(false);
          this.prompt = '';
        }
      });
  }

  copyToClipboard(message: ImageMessage): void {
    // Copy text for user messages or, if available, the base64 string for image messages.
    if (message.isUser) {
      this._clipboard.copy(message.text);
    } else if (message.imageData) {
      this._clipboard.copy(message.imageData);
    }
  }
}

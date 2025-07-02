import { Component, NgZone } from '@angular/core';
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
  imageData?: string;
}

@Component({
  selector: 'app-images',
  templateUrl: './image-gen.component.html',
  styleUrls: ['./image-gen.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ThinkingProgressComponent,
    MatIconModule
  ]
})
export class ImageGenComponent {
  messages: ImageMessage[] = [];
  prompt = '';
  error = '';
  conversationId = generateGUID();
  thinking$ = new BehaviorSubject<boolean>(false);

  constructor(
    private ngZone: NgZone,
    private ollamaService: OllamaService,
    private _clipboard: Clipboard
  ) {}

  requestImage(event: Event, prompt: string): void {
    event.preventDefault();
    if (!prompt.trim()) {
      this.error = 'Please enter a prompt for image generation.';
      return;
    }

    this.thinking$.next(true);
    this.error = '';
    this.messages.push({ isUser: true, text: prompt });

    this.ollamaService.sendImagePrompt(this.conversationId, prompt).subscribe({
      next: response => {
        console.log('🕵️‍♂️ Received response:', response);
        const raw = response.image?.trim();
        const imageData = raw?.startsWith('data:image/')
          ? raw
          : `data:image/png;base64,${raw}`;

        this.ngZone.run(() => {
          this.messages.push({
            isUser: false,
            text: '',
            imageData
          });
          this.thinking$.next(false);
        });

        console.log('🖼️ Image data prefix:', imageData.substring(0, 30) + '...');
        this.downloadBase64Image(imageData, 'generated-image.png');
      },
      error: err => {
        console.error('Error:', err);
        this.error = 'An error occurred while generating the image.';
        this.thinking$.next(false);
      }
    });
  }

  private downloadBase64Image(base64Data: string, filename: string): void {
    const dataUrl = base64Data.startsWith('data:image/')
      ? base64Data
      : `data:image/png;base64,${base64Data}`;

    const link = document.createElement('a');
    link.href = dataUrl;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  copyToClipboard(message: ImageMessage): void {
    if (message.isUser) {
      this._clipboard.copy(message.text);
    } else if (message.imageData) {
      this._clipboard.copy(message.imageData);
    }
  }
}

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
  isUser:    boolean;
  text:      string;
  imageData?: string;    // data:image/png;base64,...
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
    private ngZone: NgZone,                 // ← inject NgZone
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
  
    this.ollamaService.sendPrompt(this.conversationId, prompt, 'image')
      .subscribe({
        next: response => {
          // For JSON response, response is already parsed by Angular's HttpClient
          console.log('🕵️‍♂️ Received response:', response);
          
          if (response && response.image) {
            // Make sure the data URI has the correct prefix
            const imageData = response.image.startsWith('data:image/')
              ? response.image  // Keep as is if it already has the prefix
              : `data:image/png;base64,${response.image}`; // Add prefix if needed
            
            this.ngZone.run(() => {
              this.messages.push({
                isUser: false,
                text: '',
                imageData: imageData
              });
              this.thinking$.next(false);
            });
            
            console.log('🖼️ Image data prefix:', imageData.substring(0, 30) + '...');
            
            // Optional: If you still want to offer download functionality
            this.downloadBase64Image(response.image, 'generated-image.png');
          } else {
            console.error('Invalid response format:', response);
            this.error = 'Invalid response format from server.';
            this.thinking$.next(false);
          }
        },
        error: err => {
          console.error('Error:', err);
          this.error = 'An error occurred while generating the image.';
          this.thinking$.next(false);
        }
      });
  }
  
  // Helper method to download base64 image
  private downloadBase64Image(base64Data: string, filename: string): void {
    // Create a link element
    const link = document.createElement('a');
    
    // Make sure the base64 data doesn't already have the prefix
    const dataUrl = base64Data.startsWith('data:image/')
      ? base64Data
      : `data:image/png;base64,${base64Data}`;
    
    // Set link properties
    link.href = dataUrl;
    link.download = filename;
    
    // Append to document, click and remove
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

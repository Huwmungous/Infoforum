import { Component } from '@angular/core';
import { OllamaService } from '../ollama.service';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input'; 
import { FormatResponsePipe } from './format-response-pipe';

@Component({
  selector: 'app-deepseek',
  templateUrl: './deepseek.component.html',
  styleUrls: ['./deepseek.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    FormsModule,
    MatFormFieldModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatInputModule,
    FormatResponsePipe
  ]
})
export class DeepseekComponent {
  prompt: string = '';
  response: string = '';  // will contain the raw markdown text
  loading: boolean = false;
  error: string = '';

  constructor(private ollamaService: OllamaService) {}

  onSubmit() {
    if (!this.prompt.trim()) {
      this.error = 'Please enter a prompt.';
      return;
    }

    this.loading = true;
    this.error = '';
    this.response = ''; // Clear previous response

    this.ollamaService.sendPrompt(this.prompt)
      .subscribe({
        next: (chunk: string) => {
          console.log('Chunk:', chunk); 
          this.response += chunk;
        },
        error: (err) => {
          console.error('Error:', err);
          this.error = 'An error occurred while processing your request.';
          this.loading = false;
        },
        complete: () => {
          this.loading = false;
        }
      });
  }
}

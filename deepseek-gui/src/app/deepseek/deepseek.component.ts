import { Component } from '@angular/core';
import { OllamaService } from '../ollama.service';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

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
    MatInputModule
  ]
})
export class DeepseekComponent {
  prompt: string = '';
  response: string = '';
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
    this.response = '';

    this.ollamaService.sendPrompt(this.prompt).subscribe({
      next: (data) => {
        // Assuming the API returns a JSON object with a property "response"
        this.response = data.response || JSON.stringify(data, null, 2);
        this.loading = false;
      },
      error: (err) => {
        console.error('Error:', err);
        this.error = 'An error occurred while processing your request.';
        this.loading = false;
      }
    });
  }
}
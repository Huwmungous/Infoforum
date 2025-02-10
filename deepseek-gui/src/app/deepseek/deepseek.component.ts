import { Component, AfterViewChecked } from '@angular/core';
import { OllamaService } from '../ollama.service';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';  
import { MatIconModule } from '@angular/material/icon';
import { mapDeepseekToHighlight } from './deepseek-to-highlight-map';
import hljs from 'highlight.js';

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
    MatIconModule
  ]
})
export class DeepseekComponent implements AfterViewChecked {
  prompt: string = '';
  response: string = '';
  sections: { type: string, content: string, language?: string }[] = [];
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
    this.sections = []; // Clear previous sections

    this.ollamaService.sendPrompt(this.prompt)
      .subscribe({
        next: (chunk: string) => {
          console.log('Chunk:', chunk);
          this.response += chunk;
          this.processResponse();
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

  processResponse() {
    const parts = this.response.split(/(```[\s\S]*?```)/g);
    this.sections = parts.map(part => {
      if (part.startsWith('```') && part.endsWith('```')) {
        const content = part.slice(3, -3).trim();
        const firstLineEnd = content.indexOf('\n');
        const language = content.slice(0, firstLineEnd).trim();
        const code = content.slice(firstLineEnd + 1).trim();
        return { type: 'code', content: code, language: mapDeepseekToHighlight(language) };
      } else {
        return { type: 'text', content: part.trim() };
      }
    });
  }

  copyToClipboard(content: string) {
    navigator.clipboard.writeText(content).then(() => {
      console.log('Copied to clipboard');
    }).catch(err => {
      console.error('Could not copy text: ', err);
    });
  }

  ngAfterViewChecked() {
    this.highlightCode();
  }

  highlightCode() {
    document.querySelectorAll('pre code').forEach((block) => {
      hljs.highlightBlock(block as HTMLElement);
    });
  }
}
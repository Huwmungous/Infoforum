import { Component, AfterViewChecked } from '@angular/core';
import { OllamaService } from '../../ollama.service';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';  
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs'; 
import { MatExpansionModule } from '@angular/material/expansion'; // Import MatExpansionModule
import { mapDeepseekToHighlight } from '../../deepseek/deepseek-to-highlight-map';

declare const hljs: any;

@Component({
  selector: 'app-code-gen',
  standalone: true,
  imports: [    
      CommonModule,
      MatCardModule,
      FormsModule,
      MatFormFieldModule,
      MatProgressSpinnerModule,
      MatProgressBarModule,
      MatInputModule, 
      MatIconModule,
      MatTabsModule,
      MatExpansionModule // Include MatExpansionModule
  ],
  templateUrl: './code-gen.component.html',
  styleUrls: ['./code-gen.component.scss']
})
export class CodeGenComponent implements AfterViewChecked {

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

    // Add the prompt as a section
    this.sections.push({ type: 'prompt', content: this.prompt });

    this.ollamaService.sendPrompt(this.prompt, 'code')
      .subscribe({
        next: (chunk: string) => {
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
    const parts = this.response.split(/(```[\s\S]*?```|<think>[\s\S]*?<\/think>)/g);
    this.sections = this.sections.concat(parts.map(part => {
      if (part.startsWith('```') && part.endsWith('```')) {
        const content = part.slice(3, -3).trim();
        const firstLineEnd = content.indexOf('\n');
        const language = content.slice(0, firstLineEnd).trim();
        const code = content.slice(firstLineEnd + 1).trim();
        return { type: 'code', content: code, language: mapDeepseekToHighlight(language) };
      } else if (part.startsWith('<think>') && part.endsWith('</think>')) {
        const content = part.slice(7, -8).trim();
        return { type: 'think', content: content };
      } else {
        return { type: 'text', content: part.trim() };
      }
    }));
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
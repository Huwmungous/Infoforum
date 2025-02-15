import { Component, AfterViewChecked, ViewChildren, QueryList } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { OllamaService } from '../../ollama.service';
import { CodeGenResponseComponent } from './code-gen-response/code-gen-response.component';

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
    MatInputModule,
    MatIconModule,
    MatExpansionModule,
    CodeGenResponseComponent
  ],
  templateUrl: './code-gen.component.html',
  styleUrls: ['./code-gen.component.scss']
})
export class CodeGenComponent implements AfterViewChecked {
  @ViewChildren(CodeGenResponseComponent) codeGenResponses!: QueryList<CodeGenResponseComponent>;

  prompt: string = '';
  responses: { response: string }[] = [];
  loading: boolean = false;
  error: string = '';
  conversationId: string = generateGUID(); // Generate a GUID for the conversationId

  constructor(private ollamaService: OllamaService) {}

  onSubmit() {
    if (!this.prompt.trim()) {
      this.error = 'Please enter a prompt.';
      return;
    }

    this.loading = true;
    this.error = '';

    const newResponse = { response: '' };
    this.responses.unshift(newResponse); // Insert at the beginning of the array

    this.ollamaService.sendPrompt(this.conversationId, this.prompt, 'code')
      .subscribe({
        next: (chunk: string) => {
          newResponse.response += chunk;
          this.codeGenResponses.first.processChunk(chunk); // Process the first response
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

  removeResponse(index: number) {
    this.responses.splice(index, 1);
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

export function generateGUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}
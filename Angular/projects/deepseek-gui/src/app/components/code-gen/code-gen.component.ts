import { Component, AfterViewInit, ViewChildren, QueryList, OnInit, HostListener, ViewChild, ElementRef } from '@angular/core';
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
import { ThinkingProgressComponent } from '../thinking-progress/thinking-progress.component';
import { DEFAULT_QUOTATION, QuotationService } from '../../quotation.service';

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
    CodeGenResponseComponent,
    ThinkingProgressComponent
  ],
  templateUrl: './code-gen.component.html',
  styleUrls: ['./code-gen.component.scss']
})
export class CodeGenComponent implements AfterViewInit, OnInit {
  
  @ViewChildren(CodeGenResponseComponent) codeGenResponses!: QueryList<CodeGenResponseComponent>;
  @ViewChild('inputArea') inputArea!: ElementRef;

  prompt: string = '';
  responses: { response: string }[] = [];
  loading: boolean = false;
  error: string = '';
  conversationId: string = generateGUID(); // Generate a GUID for the conversationId
  placeholderText: string = 'Ask away!'; // Placeholder text
  placeholderLabel: string = 'Ask away'; // Label text

  private isDragging = false;
  private startX = 0;
  private startWidth = 0;

  constructor(private ollamaService: OllamaService, private quotationService: QuotationService) {}

  get quoteOfTheDay(): string {
    return localStorage.getItem('quoteOfTheDay') || DEFAULT_QUOTATION;
  }

  set quoteOfTheDay(quote: string) {
    const cleanedQuote = quote.replace(/<think>.*?<\/think>/gs, '');
    localStorage.setItem('quoteOfTheDay', cleanedQuote);
  }

  ngAfterViewInit() {
    this.codeGenResponses.changes.subscribe(() => { });
    this.inputArea.nativeElement.focus(); // Set focus on the textarea when the component loads
  }

  ngOnInit() {
    this.quotationService.quoteReceived.subscribe({
      next: (quotation: string) => {
        this.quoteOfTheDay = quotation;
      },
      error: (err: any) => {
        console.error('Error receiving quote of the day:', err);
      }
    });
  }

  onFocus() {
    this.placeholderText = '';
    this.placeholderLabel = 'Ask away (shift+enter for newline)'; 
  }

  onBlur() {
    this.placeholderText = 'Ask away!';  
    this.placeholderLabel = 'Ask away (shift+enter for newline)';  
  }

  onKeyDown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault(); 
      this.onSubmit(); 
    }
  }

  onSubmit() {
    if (!this.prompt.trim()) {
      this.error = 'Please ask a question first.';
      return;
    }

    this.loading = true;
    this.error = '';

    const newResponse = { response: '' };
    this.responses.unshift(newResponse);

    this.ollamaService.sendPrompt(this.conversationId, this.prompt, 'code')
      .subscribe({
        next: (chunk: string) => {
          newResponse.response += chunk;
          const firstResponseComponent = this.codeGenResponses.first;
          if (firstResponseComponent) {
            firstResponseComponent.processChunk(chunk);
            firstResponseComponent.prompt = this.prompt; 
          }
          this.prompt = '';
          this.inputArea.nativeElement.focus(); // Set focus back to the textarea after sending the prompt
        },
        error: (err) => {
          console.error('Error:', err);
          this.error = 'An error occurred while processing your request.';
          this.loading = false;
          this.inputArea.nativeElement.focus(); // Set focus back to the textarea in case of error
        },
        complete: () => {
          this.loading = false;
        }
      });
  }

  removeResponse(index: number) {
    this.responses.splice(index, 1);
  }

  onMouseDown(event: MouseEvent) {
    this.isDragging = true;
    this.startX = event.clientX;
    this.startWidth = document.querySelector('.left-pane')!.clientWidth;
    document.addEventListener('mousemove', this.onMouseMove);
    document.addEventListener('mouseup', this.onMouseUp);
  }

  onMouseMove = (event: MouseEvent) => {
    if (!this.isDragging) return;
    const dx = event.clientX - this.startX;
    const newWidth = this.startWidth + dx;
    (document.querySelector('.left-pane') as HTMLElement)!.style.flex = `0 0 ${newWidth}px`;
  };

  onMouseUp = () => {
    this.isDragging = false;
    document.removeEventListener('mousemove', this.onMouseMove);
    document.removeEventListener('mouseup', this.onMouseUp);
  };

  @HostListener('window:mouseup', ['$event'])
  onWindowMouseUp(event: MouseEvent) {
    if (this.isDragging) {
      this.onMouseUp();
    }
  }
}

export function generateGUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}
import {
  Component, AfterViewInit, ViewChildren, QueryList, OnInit,
  HostListener, ViewChild, ElementRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { BehaviorSubject, Observable } from 'rxjs';
import { OllamaService } from '../../ollama.service';
import { CodeGenResponseComponent } from './code-gen-response/code-gen-response.component';
import { ThinkingProgressComponent } from '../thinking-progress/thinking-progress.component';
import { DEFAULT_QUOTATION, QuotationService } from '../../quotation.service';

interface ChatChunk {
  response: string;
  done: boolean;
  // Add other properties if needed
}

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

  prompt = '';
  responses: { prompt: string; response: string }[] = [];
  thinking$ = new BehaviorSubject<boolean>(false);
  error = '';
  conversationId: string = generateGUID();
  placeholderText = 'Ask away!';
  placeholderLabel = 'Ask away';

  quoteOfTheDay$!: Observable<string>;

  private isDragging = false;
  private startX = 0;
  private startWidth = 0;

  constructor(private ollamaService: OllamaService, private quotationService: QuotationService) {}

  ngOnInit() {
    this.quoteOfTheDay$ = this.quotationService.quote$;
  }

  ngAfterViewInit() {
    this.codeGenResponses.changes.subscribe(() => {});
    setTimeout(() => this.inputArea.nativeElement.focus());
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
    const trimmed = this.prompt.trim();
    if (!trimmed) {
      this.error = 'Please ask a question first.';
      return;
    }

    this.thinking$.next(true);
    this.error = '';

    const newResponse = { prompt: trimmed, response: '' };
    this.responses.unshift(newResponse);
    this.prompt = '';
    setTimeout(() => this.inputArea.nativeElement.focus());

    setTimeout(() => {
      const targetComponent = this.codeGenResponses.first;
      if (!targetComponent) {
        this.error = 'Unable to attach response component.';
        this.thinking$.next(false);
        return;
      }

      targetComponent.prompt = newResponse.prompt;

      this.ollamaService.sendPrompt(this.conversationId, newResponse.prompt, 'code')
        .subscribe({
          next: (chunk: ChatChunk) => {  // <--- Correct typing here
            newResponse.response += chunk.response;
            targetComponent.processChunk(chunk);  // Pass ChatChunk now
          },
          error: (err: unknown) => {
            if (err instanceof Error) {
              console.error('Error:', err.message);
              this.error = 'An error occurred: ' + err.message;
            } else {
              console.error('Unknown error:', err);
              this.error = 'An unknown error occurred.';
            }
            this.thinking$.next(false);
            setTimeout(() => this.inputArea.nativeElement.focus());
          },
          complete: () => {
            this.thinking$.next(false);
          }
        });
    });
  }

  removeResponse(index: number): void {
    this.responses.splice(index, 1);
  }

  onMouseDown(event: MouseEvent): void {
    this.isDragging = true;
    this.startX = event.clientX;
    this.startWidth = document.querySelector('.left-pane')!.clientWidth;
    document.addEventListener('mousemove', this.onMouseMove);
    document.addEventListener('mouseup', this.onMouseUp);
  }

  onMouseMove = (event: MouseEvent): void => {
    if (!this.isDragging) return;
    const dx = event.clientX - this.startX;
    const newWidth = this.startWidth + dx;
    (document.querySelector('.left-pane') as HTMLElement).style.flex = `0 0 ${newWidth}px`;
  };

  onMouseUp = (): void => {
    this.isDragging = false;
    document.removeEventListener('mousemove', this.onMouseMove);
    document.removeEventListener('mouseup', this.onMouseUp);
  };

  @HostListener('window:mouseup', ['$event'])
  onWindowMouseUp(event: MouseEvent): void {
    if (this.isDragging) {
      this.onMouseUp();
    }
  }
}

export function generateGUID(): string {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

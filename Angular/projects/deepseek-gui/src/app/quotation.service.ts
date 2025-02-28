import { Injectable, EventEmitter } from '@angular/core';
import { OllamaService } from './ollama.service';
import { generateGUID } from './components/code-gen/code-gen.component';
import { interval } from 'rxjs';
import { startWith, switchMap, tap } from 'rxjs/operators';

const DEFAULT_PROMPT = 'Please provide a witty quotation relating to questions, research or enquiry. Include a citation but no other text.';
export const DEFAULT_QUOTATION = '"Ask and you shall receive, but only if you listen carefully. - Attributed to various sources in Zen Buddhism."';

@Injectable({
  providedIn: 'root'
})
export class QuotationService {
  quoteReceived = new EventEmitter<string>();

  conversationGuid: string = generateGUID();

  createNewConversation() { this.conversationGuid = generateGUID(); }

  constructor(private ollamaService: OllamaService) {

    // interval(60 * 60 * 1000).pipe(
    //   startWith(5), 
    //   switchMap(() => this.fetchQuotation())
    // ).subscribe();
  }

  private fetchQuotation() {
    console.log('Fetching quotation...');
    const result = { response: '' };

    return this.ollamaService.sendPrompt(this.conversationGuid, DEFAULT_PROMPT, 'chat').pipe(
      tap({
        next: (chunk: string) => {
          result.response += chunk;
        },
        error: (err) => {
          console.error('Error:', err);
        },
        complete: () => {
          this.quoteReceived.emit(result.response);
          this.createNewConversation();
        }
      })
    );
  }

  fetch() {
    this.fetchQuotation().subscribe();
  }
}
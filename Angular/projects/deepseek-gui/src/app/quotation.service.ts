import { Injectable } from '@angular/core';
import { BehaviorSubject, interval } from 'rxjs';
import { startWith, switchMap, tap } from 'rxjs/operators';
import { OllamaService } from './ollama.service';
import { generateGUID } from './components/code-gen/code-gen.component';

const DEFAULT_PROMPT = 'Please provide a witty quotation relating to questions, research or enquiry. Include a citation but no other text.';
export const DEFAULT_QUOTATION = '"Ask and you shall receive, but only if you listen carefully. - Attributed to various sources in Zen Buddhism."';

@Injectable({
  providedIn: 'root'
})
export class QuotationService {
  private readonly STORAGE_KEY = 'quoteOfTheDay';

  // BehaviorSubject holds current quote, starts with default or localStorage value
  private _quoteSubject = new BehaviorSubject<string>(
    localStorage.getItem(this.STORAGE_KEY) ?? DEFAULT_QUOTATION
  );

  // Observable to expose quote changes
  quote$ = this._quoteSubject.asObservable();

  conversationGuid: string = generateGUID();

  constructor(private ollamaService: OllamaService) {
    interval(1220 * 60 * 1000).pipe(
      // startWith(10 * 60 * 1000), // emit after 10 minutes on start
      startWith(0), // emit immediately
      switchMap(() => this.fetchQuotation())
    ).subscribe();
  }

  private fetchQuotation() {
    let accumulatedQuote = '';

    return this.ollamaService.sendPrompt(this.conversationGuid, DEFAULT_PROMPT, 'chat').pipe(
      tap({
        next: (chunk: any) => {
          if (!chunk || typeof chunk !== 'object') return;
          accumulatedQuote += chunk.response ?? '';
        },
        error: (err) => {
          console.error('Quotation fetch error:', err);
        },
        complete: () => {
          // Remove all <think>...</think> blocks
          let cleanedQuote = accumulatedQuote.replace(/<think>[\s\S]*?<\/think>/g, '').trim();
          if (!cleanedQuote) cleanedQuote = DEFAULT_QUOTATION;
  
          this._quoteSubject.next(cleanedQuote);
          localStorage.setItem(this.STORAGE_KEY, cleanedQuote);
            this.createNewConversation();
          }
      })
    );
  }


  fetch() {
    this.fetchQuotation().subscribe();
  }

  createNewConversation() {
    this.conversationGuid = generateGUID();
  }
}

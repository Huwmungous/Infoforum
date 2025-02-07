import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class OllamaService {
  private apiUrl = 'http://intelligence:5008/api/generate';

  constructor() {}

  sendPrompt(prompt: string): Observable<string> {  // ✅ Fix: Returns `Observable<string>`
    const body = JSON.stringify({ model: 'deepseek-coder:33b', prompt: prompt });

    return new Observable(observer => {
      fetch(this.apiUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: body
      })
      .then(response => {
        if (!response.body) throw new Error('No response body received.');

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let partial = '';

        const processChunk = ({ done, value }: { done: boolean, value?: Uint8Array }) => {
          if (done) {
            observer.complete();
            return;
          }

          partial += decoder.decode(value, { stream: true });
          const lines = partial.split('\n').filter(line => line.trim().length > 0);

          lines.forEach(line => {
            try {
              const json = JSON.parse(line);
              if (json.response) {
                observer.next(json.response.trim()); // ✅ Emit single response chunk
              }
            } catch (err) {
              console.warn('Skipping invalid JSON chunk:', line);
            }
          });

          reader.read().then(processChunk);
        };

        reader.read().then(processChunk);
      })
      .catch(error => observer.error(error));
    });
  }
}

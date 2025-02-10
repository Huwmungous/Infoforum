import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class OllamaService {
  private apiUrl = 'http://localhost:5008/Ollama';

  constructor() {}

  sendPrompt(prompt: string): Observable<string> {
    return new Observable(observer => {
      fetch(this.apiUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(prompt)
      })
      .then(response => {
        if (!response.body) {
          throw new Error('No response body received.');
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let partial = '';

        const processChunk = ({ done, value }: { done: boolean, value?: Uint8Array }) => {
          if (done) {
            if (partial.length > 0) {
              observer.next(partial);
            }
            observer.complete();
            return;
          }

          // Decode the new chunk and add it to the partial string.
          partial += decoder.decode(value, { stream: true });
          // Split into lines. The last line might be incomplete.
          const lines = partial.split('\n');
          // Keep the last (possibly incomplete) line for the next round.
          partial = lines.pop() || "";

          // Process each complete line without trimming whitespace.
          for (const line of lines) {
            if (line === '') continue;
            observer.next(line);
          }

          reader.read().then(processChunk);
        };

        reader.read().then(processChunk);
      })
      .catch(error => observer.error(error));
    });
  }
}

export default OllamaService;

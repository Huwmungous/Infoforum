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
        body: JSON.stringify({ prompt })
      })
      .then(response => {
        if (!response.body) {
          throw new Error('No response body received.');
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();

        const processChunk = ({ done, value }: { done: boolean, value?: Uint8Array }) => {
          if (done) {
            observer.complete();
            return;
          }

          // Decode the chunk as is (do not trim or split it)
          const chunk = decoder.decode(value, { stream: true });
          observer.next(chunk);

          reader.read().then(processChunk);
        };

        reader.read().then(processChunk);
      })
      .catch(error => observer.error(error));
    });
  }
}

export default OllamaService;

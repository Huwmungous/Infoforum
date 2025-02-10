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
            // Process any remaining data in `partial`
            if (partial.trim().length > 0) {
              observer.next(partial.trim());
            }
            observer.complete();
            return;
          }
  
          // Decode the new chunk and add to the partial string
          partial += decoder.decode(value, { stream: true });
          // Split into lines; the last line may be incomplete.
          const lines = partial.split('\n');
          // Save the last piece back to partial
          partial = lines.pop() || "";
  
          // Process all complete lines
          for (const line of lines) {
            if (line.trim().length === 0) continue;
            observer.next(line.trim());
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
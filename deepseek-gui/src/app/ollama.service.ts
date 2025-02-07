import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class OllamaService {
  private apiUrl = 'http://intelligence:5008/api/generate';

  constructor() {}

  sendPrompt(prompt: string): Observable<string> {
    const body = JSON.stringify({ model: 'deepseek-coder:33b', prompt: prompt });
  
    return new Observable(observer => {
      fetch(this.apiUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: body
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
              try {
                const json = JSON.parse(partial);
                if (json.response) {
                  observer.next(json.response.trim());
                }
              } catch (err) {
                console.warn('Could not parse final JSON chunk:', partial);
              }
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
            try {
              const json = JSON.parse(line);
              if (json.response) {
                observer.next(json.response.trim());
              }
            } catch (err) {
              console.warn('Skipping invalid JSON chunk:', line);
            }
          }
  
          reader.read().then(processChunk);
        };
  
        reader.read().then(processChunk);
      })
      .catch(error => observer.error(error));
    });
  }
  
}

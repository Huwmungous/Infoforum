import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class OllamaService {

  private apiUrl = environment.apiUrl;

  constructor() {}

  sendPrompt(prompt: string, dest: string = 'code'): Observable<string> {
    return new Observable(observer => {
      fetch(`${this.apiUrl}?dest=${dest}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(prompt) // Send only the prompt in the body
      })
      .then(response => {
        if (!response.body)
          throw new Error('No response body received.');

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        const processChunk = ({ done, value }: { done: boolean, value?: Uint8Array }) => {
          if (done) {
            observer.complete();
            return;
          }
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

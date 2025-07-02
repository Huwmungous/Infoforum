import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class OllamaService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // ✅ Streaming response for chat/code
  sendPrompt(conversationId: string, prompt: string, dest: string = 'code'): Observable<string> {
    const url = `${this.apiUrl}?conversationId=${conversationId}&dest=${dest}`;
    const headers = {
      'Content-Type': 'application/json',
      'Accept': 'text/plain'
    };

    return new Observable<string>((observer) => {
      fetch(url, {
        method: 'POST',
        headers,
        body: JSON.stringify({ prompt }) // Must match server-side DTO shape
      }).then(async response => {
        if (!response.body) {
          observer.error('No response body');
          return;
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          const chunk = decoder.decode(value, { stream: true });
          observer.next(chunk);
        }
        observer.complete();
      }).catch(err => observer.error(err));
    });
  }

  // ✅ Image generation with parsed JSON response
  sendImagePrompt(conversationId: string, prompt: string): Observable<{ image: string }> {
    const url = `${this.apiUrl}?conversationId=${conversationId}&dest=image`;
    const headers = new HttpHeaders({ 'Content-Type': 'application/json' });
    return this.http.post<{ image: string }>(url, { prompt }, { headers });
  }
}

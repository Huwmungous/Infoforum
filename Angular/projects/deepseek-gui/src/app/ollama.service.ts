import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class OllamaService {
  private apiUrl = environment.apiUrl;

  constructor(
    private oidcSecurityService: OidcSecurityService
  ) {}

  sendPrompt(conversationId: string, prompt: string, dest: string = 'code'): Observable<any> {
  const url = `${this.apiUrl}?conversationId=${conversationId}&dest=${dest}`;

  return new Observable<any>((observer) => {
    this.oidcSecurityService.getAccessToken().subscribe({
      next: (token) => {
        const headers: HeadersInit = {
          'Content-Type': 'application/json',
          'Accept': 'application/x-ndjson',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        };

        fetch(url, {
          method: 'POST',
          headers,
          body: JSON.stringify({ prompt })
        }).then(async response => {
          if (!response.ok) {
            observer.error(`HTTP error ${response.status}: ${response.statusText}`);
            return;
          }

          if (!response.body) {
            observer.error('No response body');
            return;
          }

          const reader = response.body.getReader();
          const decoder = new TextDecoder();
          let buffer = '';

          while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });

            let lines = buffer.split('\n');
            buffer = lines.pop() ?? ''; // save last incomplete line

            for (const line of lines) {
              if (line.trim() === '') continue;
              try {
                const obj = JSON.parse(line);
                observer.next(obj); // emit parsed object
                if (obj.done) {
                  observer.complete();
                  return;
                }
              } catch (err) {
                observer.error('Failed to parse NDJSON line: ' + line);
                return;
              }
            }
          }

          if (buffer.trim()) {
            try {
              const finalObj = JSON.parse(buffer);
              observer.next(finalObj);
            } catch {
              // ignore trailing junk
            }
          }

          observer.complete();
        }).catch(err => observer.error(err));
      },
      error: (err) => observer.error('Token fetch failed: ' + err)
    });
  });
}


  sendImagePrompt(conversationId: string, prompt: string): Observable<{ image: string }> {
    // Optional: implement similar token-based auth here if needed
    throw new Error('Not implemented');
  }
}

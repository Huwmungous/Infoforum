// src/app/ollama.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class OllamaService {
  // Use the API endpoint from your sample curl command.
  private apiUrl = 'http://intelligence:5008/api/generate';

  constructor(private http: HttpClient) {}

  sendPrompt(prompt: string): Observable<any> {
    // Build the payload expected by your API.
    const payload = {
      model: 'deepseek-coder:33b',
      prompt: prompt
    };
    return this.http.post<any>(this.apiUrl, payload);
  }
}

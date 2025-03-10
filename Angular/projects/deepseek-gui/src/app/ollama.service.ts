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

  sendPrompt(conversationId: string, prompt: string, dest: string = 'code'): Observable<any> {
    const url = `${this.apiUrl}?conversationId=${conversationId}&dest=${dest}`;
    const headers = new HttpHeaders({ 'Content-Type': 'application/json' });

    console.log(`sendPrompt() - url: ${url}, prompt: ${prompt}`);
    return this.http.post(url, JSON.stringify(prompt), { headers, responseType: 'text' });
  }
}
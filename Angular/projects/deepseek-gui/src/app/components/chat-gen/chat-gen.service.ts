import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { OllamaService } from '../../ollama.service';

@Injectable({
  providedIn: 'root'
})
export class ChatGenService {
  prompts: any;
  constructor(private ollamaService: OllamaService) { }

  getInitialPrompt(): Observable<string> {
    return this.ollamaService.sendPrompt('', '', 'conversation');
  }

  sendPrompt(prompt: string): Observable<string> {
    return this.ollamaService.sendPrompt(this.prompts[0], prompt, 'new_prompt');
  }
}
// File: src/app/services/code-context.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class CodeContextService {
  private folderSubject = new BehaviorSubject<string | null>(null);
  private conversationId: string = this.generateConversationId();
  folder$ = this.folderSubject.asObservable();

  constructor(private http: HttpClient) {}

  getConversationId(): string {
    return this.conversationId;
  }

  setContextFolder(path: string, branch: string) {
    const payload = { folderPath: path, branchName: branch };
    this.folderSubject.next(path);
    return this.http.post(`/api/codecontext/set-code-context?conversationId=${this.conversationId}`, payload, {
      headers: { 'Content-Type': 'application/json' },
      responseType: 'text'
    }).subscribe();
  }

  getCurrentFolder(): string | null {
    return this.folderSubject.value;
  }

  private generateConversationId(): string {
    const existing = localStorage.getItem('codeConversationId');
    if (existing) return existing;
    const id = crypto.randomUUID();
    localStorage.setItem('codeConversationId', id);
    return id;
  }
}

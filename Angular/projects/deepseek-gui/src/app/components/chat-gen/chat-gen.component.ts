import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ChatGenService } from './chat-gen.service';

@Component({
  selector: 'app-chat-gen',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  template: `
    <div>
      <h2>Chat Generation</h2>
      <ul *ngFor="let item of chatHistory; let i = index">
        <li><strong>Prompt:</strong> {{ item.prompt }}</li>
        <li *ngIf="item.response"><strong>Response:</strong> {{ item.response }}</li>
      </ul>
      <form [formGroup]="responseForm">
        <label>Response:</label>
        <input formControlName="prompt" type="text">
        <button (click)="sendPrompt()">Send Response</button>
      </form>
    </div>
  `
})
export class ChatGenComponent implements OnInit {
  chatHistory: { prompt: string, response?: string }[] = [];
  responseForm;

  constructor(private chatGenService: ChatGenService, private fb: FormBuilder) {
    this.responseForm = this.fb.group({
      prompt: ['']
    });
  }

  ngOnInit(): void {
    this.chatGenService.getInitialPrompt().subscribe(prompt => {
      this.chatHistory.push({ prompt });
    });
  }

  sendPrompt(): void {
    const prompt = this.responseForm.value.prompt || '';
    this.chatGenService.sendPrompt(prompt).subscribe(response => {
      this.chatHistory.push({ prompt, response });
      this.responseForm.reset();
    });
  }
}
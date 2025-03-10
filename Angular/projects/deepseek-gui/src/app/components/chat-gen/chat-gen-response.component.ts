import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-chat-gen-response',
  standalone: true,
  imports:[CommonModule],
  template: `
    <li *ngIf="response"><strong>Response:</strong> {{ response }}</li>
  `
})
export class ChatGenResponseComponent {
  @Input() response!: string;
}
import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-chat-gen-prompt',
  standalone: true,
  imports:[CommonModule],
  template: `
    <li><strong>Prompt:</strong> {{ prompt }}</li>
  `
})
export class ChatGenPromptComponent {
  @Input() prompt: string = '';
}
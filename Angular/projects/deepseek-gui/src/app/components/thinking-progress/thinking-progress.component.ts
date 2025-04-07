import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-thinking-progress',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div *ngIf="active" class="thinking-overlay">
      <img src="assets/thinking.svg" alt="Thinking..." class="thinking-svg" />
    </div>
  `,
  styleUrls: ['./thinking-progress.component.scss']
})
export class ThinkingProgressComponent {
  @Input() active: boolean = false;
}
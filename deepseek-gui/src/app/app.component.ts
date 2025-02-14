import { Component } from '@angular/core';
import { IntelligenceComponent } from './deepseek/intelligence.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [IntelligenceComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})

export class AppComponent {
  title = 'Intelligence GUI';
}

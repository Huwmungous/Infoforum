import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DeepseekComponent } from "./deepseek/deepseek.component";

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [DeepseekComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})

export class AppComponent {
  title = 'deepseek-gui';
}

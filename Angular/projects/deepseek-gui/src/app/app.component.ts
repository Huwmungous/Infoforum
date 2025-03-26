import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { IntelligenceComponent } from "./deepseek/intelligence.component";

@Component({
  selector: 'app-root',
  template: `<app-intelligence></app-intelligence>`,
  standalone: true,
  imports: [RouterModule, IntelligenceComponent]
})
export class AppComponent {}

import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { IntelligenceComponent } from "./deepseek/intelligence.component";

@Component({
  selector: 'app-root',
  template: `<router-outlet></router-outlet>`,
  standalone: true,
  imports: [RouterModule]
})
export class AppComponent {}

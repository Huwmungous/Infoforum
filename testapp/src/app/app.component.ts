import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LogoutService } from 'ifauth-lib';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'testapp';

  constructor( private logoutService: LogoutService ) { }

  logout() { 
    this.logoutService.logout();  
  }
}

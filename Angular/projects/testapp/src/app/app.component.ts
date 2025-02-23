import { Component } from '@angular/core';
import { LogoutService } from 'ifauth-lib';

@Component({
    selector: 'app-root',
    imports: [],
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

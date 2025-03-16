import { Component, OnInit } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Component({
  selector: 'app-silent-renew',
  template: '',
  styleUrls: [ ],
  standalone: true,
})
export class SilentRenewComponent implements OnInit {

  constructor(private oidcSecurityService: OidcSecurityService) {}

  ngOnInit() {
    this.oidcSecurityService.checkAuth().subscribe(({ isAuthenticated }) => {
      console.log('Silent Renew - Auth Result:', isAuthenticated);
      
      if (isAuthenticated) {
        // Redirect user to the home page or any other place after successful authentication
      } else {
        this.oidcSecurityService.authorize();
      }
    });
  }
}
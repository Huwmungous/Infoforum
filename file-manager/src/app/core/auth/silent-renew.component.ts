import { Component, OnInit } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Component({
  selector: 'app-silent-renew',
  template: '',
  styleUrls: [ ],
  standalone: true,
})
export class SilentRenewComponent implements OnInit {

  constructor(private oidc: OidcSecurityService) {}

  ngOnInit() {
    this.oidc.checkAuth().subscribe(({ isAuthenticated }) => {
      console.log('Silent Renew - Auth Result:', isAuthenticated);
      if (!isAuthenticated) {
        this.oidc.authorize();
      }
    });
  }
}
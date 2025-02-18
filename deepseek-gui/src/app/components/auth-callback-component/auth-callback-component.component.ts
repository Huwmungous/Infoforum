// callback.component.ts
import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Component({
  selector: 'app-callback',
  template: '<p>Processing login...</p>',
})
export class AuthCallbackComponent implements OnInit {
  constructor(
    private oidcSecurityService: OidcSecurityService,
    private router: Router
  ) {}

  ngOnInit() {
    this.oidcSecurityService.checkAuth().subscribe(({ isAuthenticated }) => {
      console.log('callback component - app authenticated :', isAuthenticated);
      if (isAuthenticated) {
        this.router.navigate(['/home']);
      } else {
        // Optionally handle the error scenario here
        console.error('Authentication failed');
      }
    });
  }
}

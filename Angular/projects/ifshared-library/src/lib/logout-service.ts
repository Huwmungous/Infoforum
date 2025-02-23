import { Injectable } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class LogoutService {
  constructor(private oidcSecurityService: OidcSecurityService) {}

  logout(): void {
    this.oidcSecurityService.getIdToken().subscribe(idToken => {
      if (idToken) {
        this.oidcSecurityService.logoff(idToken);
      } else {
        console.error('ID token is missing');
      }
    });
    
  }
}

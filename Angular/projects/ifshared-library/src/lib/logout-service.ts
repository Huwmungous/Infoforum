import { Injectable } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class LogoutService {
  constructor(private oidcSecurityService: OidcSecurityService) {}

  logout(): void {
    this.oidcSecurityService.logoffAndRevokeTokens().subscribe(() => {
      window.location.href = window.location.origin; 
    });
  }
}

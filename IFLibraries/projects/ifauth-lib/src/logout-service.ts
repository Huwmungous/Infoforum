// src/logout-service.ts

import { Injectable } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class LogoutService {
  constructor(private oidcSecurityService: OidcSecurityService) {}

  logout(): void {
    this.oidcSecurityService.getIdToken().subscribe(idToken => {
      this.oidcSecurityService.logoffAndRevokeTokens().subscribe(() => {
        this.oidcSecurityService.logoff( idToken  );
      });
    });
  }

}

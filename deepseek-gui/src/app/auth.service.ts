import { Injectable } from '@angular/core';
import { OAuthService } from 'angular-oauth2-oidc';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  constructor(private oauthService: OAuthService) {}

  /** Initiates the login flow */
  login() {
    // Starts the login flow by redirecting to the OIDC provider.
    this.oauthService.initLoginFlow();
  }

  /** Logs out the user */
  logout() {
    this.oauthService.logOut();
  }

  /** Checks whether the user is logged in */
  isLoggedIn(): boolean {
    return this.oauthService.hasValidAccessToken();
  }

  /** Returns user claims (profile info) */
  get identityClaims() {
    return this.oauthService.getIdentityClaims();
  }
}


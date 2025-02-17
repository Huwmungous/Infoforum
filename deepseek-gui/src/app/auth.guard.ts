import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { OAuthService } from 'angular-oauth2-oidc';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {

  constructor(private oauthService: OAuthService) {}

  canActivate(): boolean { 
    console.log(this.oauthService);
    console.log('Access Token :', this.oauthService.hasValidAccessToken());
    return this.oauthService.hasValidAccessToken();
  }
}
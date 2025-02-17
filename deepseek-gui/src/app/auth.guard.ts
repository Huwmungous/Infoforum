import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router'; 
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {

  constructor(private oidcSecurityService: OidcSecurityService, private router: Router) {}

  canActivate(): boolean {
    console.log('AuthGuard#canActivate called');
    console.log('isAuthenticated: ', this.oidcSecurityService.isAuthenticated());
    debugger;
    return true;
    // if (this.oidcSecurityService.isAuthenticated()) {
    //   return true;
    // } else {
    //   this.oidcSecurityService.authorize();
    //   return false;
    // }
  }
}
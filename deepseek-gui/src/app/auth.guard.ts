// auth.guard.ts
import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  constructor(
    private oidcSecurityService: OidcSecurityService,
    private router: Router
  ) {}

  canActivate(
    next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      map(({ isAuthenticated }) => {
        if (isAuthenticated) {
          return true;
        } else {
          this.oidcSecurityService.authorize();
          return false;
        }
      })
    );
  }
}
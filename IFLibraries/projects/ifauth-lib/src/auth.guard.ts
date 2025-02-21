import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable } from 'rxjs';
import { take, tap, map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  private isAuthProcessInProgress = false;

  constructor( private oidcSecurityService: OidcSecurityService ) {}

  canActivate(next: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      take(1),
      tap(({ isAuthenticated }) => {
        if (!isAuthenticated && !this.isAuthProcessInProgress) {
          this.isAuthProcessInProgress = true;
          this.oidcSecurityService.authorize();
        }
      }),
      map(({ isAuthenticated }) => isAuthenticated)
    );
  }
}

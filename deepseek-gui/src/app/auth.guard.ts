import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { map, switchMap, take, tap } from 'rxjs/operators';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  private isAuthProcessInProgress = false;

  constructor(
    private oidcSecurityService: OidcSecurityService,
    private router: Router
  ) {}

  canActivate(next: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      take(1), // Ensures we only check once
      tap(({ isAuthenticated }) => console.log('Auth Guard - Authenticated:', isAuthenticated)),
      map(({ isAuthenticated }) => {
        if (!isAuthenticated) {
          if (!this.isAuthProcessInProgress) {
            console.warn('Auth Guard: Not authenticated, initiating login...');
            this.isAuthProcessInProgress = true;
            this.oidcSecurityService.authorize();
          }
          return false;
        }
        return true;
      })
    );
  }
}

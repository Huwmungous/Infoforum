// auth.guard.ts
import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
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
      switchMap(({ isAuthenticated, accessToken }: { isAuthenticated: boolean; accessToken: string }) => {
        console.log('Auth Guard - Authenticated:', isAuthenticated);
        console.log('Auth Guard - Access Token:', accessToken);
  
        if (isAuthenticated) {
          return of(true);
        } else {
          console.warn('Auth Guard: Not authenticated, waiting...');
          this.oidcSecurityService.authorize();
          return of(false);
        }
      })
    );
  }
  
  
}
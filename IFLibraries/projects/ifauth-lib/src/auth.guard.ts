import { Injectable } from '@angular/core';
import { 
  CanActivate, 
  CanActivateChild, 
  CanLoad, 
  ActivatedRouteSnapshot, 
  RouterStateSnapshot, 
  Route, 
  UrlSegment 
} from '@angular/router';
import { Observable } from 'rxjs';
import { take, tap, map } from 'rxjs/operators';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate, CanActivateChild, CanLoad {
  private isAuthProcessInProgress = false;

  constructor(private oidcSecurityService: OidcSecurityService) {}

  /**
   * Centralized authentication check.
   * Calls the OIDC security service to determine if the user is authenticated.
   * If not authenticated and not already processing an auth request, initiates login.
   */
  private checkAuthentication(): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      take(1),
      tap(({ isAuthenticated }) => {
        if (!isAuthenticated && !this.isAuthProcessInProgress) {
          console.warn('Auth Guard: Not authenticated, initiating login...');
          this.isAuthProcessInProgress = true;
          this.oidcSecurityService.authorize();
        }
      }),
      map(({ isAuthenticated }) => isAuthenticated)
    );
  }

  /**
   * CanActivate implementation.
   * Called to determine if a route can be activated.
   */
  canActivate(
    next: ActivatedRouteSnapshot, 
    state: RouterStateSnapshot
  ): Observable<boolean> {
    return this.checkAuthentication();
  }

  /**
   * CanActivateChild implementation.
   * Called to determine if a child route can be activated.
   */
  canActivateChild(
    childRoute: ActivatedRouteSnapshot, 
    state: RouterStateSnapshot
  ): Observable<boolean> {
    return this.checkAuthentication();
  }

  /**
   * CanLoad implementation.
   * Called to determine if a lazy-loaded module can be loaded.
   */
  canLoad(
    route: Route, 
    segments: UrlSegment[]
  ): Observable<boolean> {
    return this.checkAuthentication();
  }
}

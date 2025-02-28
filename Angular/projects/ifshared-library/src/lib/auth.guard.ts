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
import { AuthConfigService } from './auth-config.service';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate, CanActivateChild, CanLoad {
  private isAuthProcessInProgress = false;

  constructor(
    private oidcSecurityService: OidcSecurityService,
    private configService: AuthConfigService) {}

  private checkAuthentication(): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      take(1),
      tap(({ isAuthenticated }) => {
        if (!isAuthenticated && !this.isAuthProcessInProgress) {
          console.warn('Auth Guard: Not authenticated, initiating login...');
          this.isAuthProcessInProgress = true;
          this.oidcSecurityService.authorize(this.configService.configId);
        }
      }),
      map(({ isAuthenticated }) => isAuthenticated)
    );
  }

  canActivate(
    next: ActivatedRouteSnapshot, 
    state: RouterStateSnapshot
  ): Observable<boolean> {
    return this.checkAuthentication();
  }
  
  canActivateChild(
    childRoute: ActivatedRouteSnapshot, 
    state: RouterStateSnapshot
  ): Observable<boolean> {
    return this.checkAuthentication();
  }
  
  canLoad(
    route: Route, 
    segments: UrlSegment[]
  ): Observable<boolean> {
    return this.checkAuthentication();
  }
}
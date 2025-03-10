import { Injectable } from '@angular/core';
import { 
  CanActivate, 
  CanActivateChild,
  ActivatedRouteSnapshot, 
  RouterStateSnapshot
} from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthConfigService } from './auth-config.service';
import { Observable } from 'rxjs';
import { take, tap, map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate, CanActivateChild {
  private isAuthProcessInProgress = false;

  constructor(
    private oidcSecurityService: OidcSecurityService,
    private configService: AuthConfigService) {}

  private checkAuthentication(): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      take(1),
      tap((response: { isAuthenticated: boolean }) => {
        if (!response.isAuthenticated && !this.isAuthProcessInProgress) {
          console.warn('Auth Guard: Not authenticated, initiating login...');
          this.isAuthProcessInProgress = true;
          // Persist only the minimal keys needed
          localStorage.setItem('selectedConfigId', this.configService.configId);
          // Optionally, if you need to persist the clientId:
          localStorage.setItem('selectedClientId', '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7');
          
          this.oidcSecurityService.authorize(this.configService.configId);
        }
      }),
      map((response: { isAuthenticated: boolean }) => response.isAuthenticated)
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
}

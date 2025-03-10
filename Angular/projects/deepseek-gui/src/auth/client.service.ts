import { Injectable, EventEmitter } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthConfigService, KEYCLOAK_BASE_URL } from './auth-config.service';

@Injectable({ providedIn: 'root' })
export class ClientService {

  afterLoginEvent: EventEmitter<{ realm: string, client: string }> = new EventEmitter<{ realm: string, client: string }>();
  afterLogoutEvent: EventEmitter<{ realm: string, client: string }> = new EventEmitter<{ realm: string, client: string }>();

  constructor(
    private oidcSecurityService: OidcSecurityService,
    private configService: AuthConfigService) {}

  login(configId: number = 1): void { 
    this.logout(); 
    this.oidcSecurityService.authorize(configId.toString());
    this.afterLoginEvent.emit({ realm: this.configService.realm, client:  this.configService.client });
  }

  logout(configId: string = '1'): void { 
    
    // First check if authenticated
    this.oidcSecurityService.isAuthenticated(configId).subscribe((isAuthenticated) => {
      if (isAuthenticated) {
        // Try to properly logout with token revocation
        this.oidcSecurityService.logoffAndRevokeTokens(configId).subscribe({
          next: () => {
            console.log('Logout successful with token revocation');
            this.handleSuccessfulLogout();
          },
          error: (error) => {
            console.warn('Error during logoffAndRevokeTokens:', error);
            
            // Fallback to basic logout without revocation
            console.log('Attempting fallback logout method');
            this.oidcSecurityService.logoff(configId).subscribe({
              next: () => {
                console.log('Fallback logout successful');
                this.handleSuccessfulLogout();
              },
              error: (fallbackError) => {
                console.error('Fallback logout also failed:', fallbackError);
                
                // Last resort: clear local auth data and redirect
                this.forceLogout();
              }
            });
          }
        });
      } else {
        console.log('User already logged out');
        this.forceLogout();
      }
    });
  }
  
  private handleSuccessfulLogout(): void {
    // Clear any local auth data
    this.clearLocalAuthData();
    
    // Redirect to home and emit event
    window.location.href = window.location.origin;
    this.afterLogoutEvent.emit({ 
      realm: this.configService.realm, 
      client: this.configService.client 
    });
  }
  
  private forceLogout(): void {
    // Force clear any local auth data
    this.clearLocalAuthData();
    
    // Redirect to Keycloak logout endpoint directly as a last resort
    const keycloakLogoutUrl = `${KEYCLOAK_BASE_URL}${this.configService.realm}/protocol/openid-connect/logout`;
    window.location.href = keycloakLogoutUrl;
  }
  
  private clearLocalAuthData(): void {
    // Find and remove all auth-related items from storage
    const authPrefix = 'app-auth-';
    
    // Clear localStorage
    for (let i = localStorage.length - 1; i >= 0; i--) {
      const key = localStorage.key(i);
      if (key && key.includes(authPrefix)) {
        localStorage.removeItem(key);
      }
    }
    
    // Clear sessionStorage
    for (let i = sessionStorage.length - 1; i >= 0; i--) {
      const key = sessionStorage.key(i);
      if (key && key.includes(authPrefix)) {
        sessionStorage.removeItem(key);
      }
    }
  }
  

}
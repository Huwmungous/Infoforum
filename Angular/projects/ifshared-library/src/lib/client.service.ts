import { Injectable } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { provideAuth } from './provideAuth';

export const DEFAULT_REALM = 'LongmanRd';
export const DEFAULT_CLIENT = '9F32F055-D2FF-4461-A47B-4A2FCA6720DA';

@Injectable({
  providedIn: 'root'
})
export class ClientService {
  private realm: string = '';
  private client: string = '';

  constructor(private oidcSecurityService: OidcSecurityService) {}

  setClient(realm: string, client: string) {
    if (this.client !== client) {
        console.log('ClientService: Setting client to ', realm, client);
      this.realm = realm;
      this.client = client;
      this.reinitializeAuth();
    }
  }

  reinitializeAuth() {
    const realm = this.realm == 'Default' ? DEFAULT_REALM : this.realm;
    const client = this.realm == 'Default' ? DEFAULT_CLIENT : this.client;
    provideAuth(realm, client);
  }

  isAuthenticated(): boolean {
    return this.oidcSecurityService.isAuthenticated() ? 
      true : 
      false;
  }

  login(): void {
    if(this.oidcSecurityService.isAuthenticated())
        this.logout();
    
    this.oidcSecurityService.authorize();  
  }

  logout(): void {
    if(this.oidcSecurityService.isAuthenticated()) {
      this.oidcSecurityService.logoffAndRevokeTokens().subscribe(() => {
        window.location.href = window.location.origin; 
      });
    }
  }
}
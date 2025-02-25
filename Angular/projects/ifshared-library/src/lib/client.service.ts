import { Injectable, EventEmitter } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { provideAuth } from './provideAuth';

export const DEFAULT_REALM = 'LongmanRd';
export const DEFAULT_CLIENT = '9F32F055-D2FF-4461-A47B-4A2FCA6720DA';

@Injectable({
  providedIn: 'root'
})
export class ClientService {
  private realm: string = DEFAULT_REALM;
  private client: string = DEFAULT_CLIENT;
  afterLoginEvent: EventEmitter<{ realm: string, client: string }> = new EventEmitter<{ realm: string, client: string }>();

  constructor(private oidcSecurityService: OidcSecurityService) {}

  setClient(realm: string, client: string) {
    console.log('ClientService: Setting client to ', realm, client);
    if(this.realm !== realm || this.client !== client) {
      this.realm = realm;
      this.client = client; 
      provideAuth(this.realm, this.client);
    }
  }

  isAuthenticated(): boolean {
    return this.oidcSecurityService.isAuthenticated() ? true : false;
  }

  login(): void { 
    this.logout(); 
    this.oidcSecurityService.authorize();
    this.afterLoginEvent.emit({ realm: this.realm, client: this.client });
  }

  logout(): void {
    if (this.oidcSecurityService.isAuthenticated()) {
      this.oidcSecurityService.logoffAndRevokeTokens().subscribe(() => {
        window.location.href = window.location.origin;
      });
    }
  }
}
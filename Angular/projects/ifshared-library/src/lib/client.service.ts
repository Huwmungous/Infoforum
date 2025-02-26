import { Injectable, EventEmitter } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { provideAuth } from './auth-config.service';

export const DEFAULT_AUTHORITY = 'LongmanRd';
export const DEFAULT_REALM = 'LongmanRd';
export const DEFAULT_CLIENT = '9F32F055-D2FF-4461-A47B-4A2FCA6720DA';

@Injectable({
  providedIn: 'root'
})
export class ClientService {
  private realm: string = DEFAULT_REALM;
  private client: string = DEFAULT_CLIENT;

  afterLoginEvent: EventEmitter<{ realm: string, client: string }> = new EventEmitter<{ realm: string, client: string }>();
  afterLogoutEvent: EventEmitter<{ realm: string, client: string }> = new EventEmitter<{ realm: string, client: string }>();

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

  login(configId: number = 1): void { 
    this.logout(); 
    this.oidcSecurityService.authorize(configId.toString());
    this.afterLoginEvent.emit({ realm: this.realm, client: this.client });
  }

  logout(configId: number = 1): void {
    const cfg = configId.toString();
    if (this.oidcSecurityService.isAuthenticated(cfg)) {
      this.oidcSecurityService.logoffAndRevokeTokens(cfg).subscribe(() => {
        window.location.href = window.location.origin;
        this.afterLogoutEvent.emit({ realm: this.realm, client: this.client });
      });
    }
  }
}
import { Injectable, EventEmitter } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthConfigService } from './auth-config.service';

@Injectable({ providedIn: 'root' })
export class ClientService {

  afterLoginEvent: EventEmitter<{ realm: string, client: string }> = new EventEmitter<{ realm: string, client: string }>();
  afterLogoutEvent: EventEmitter<{ realm: string, client: string }> = new EventEmitter<{ realm: string, client: string }>();

  constructor(
    private oidcSecurityService: OidcSecurityService,
    private configService: AuthConfigService) {}

  isAuthenticated(): boolean {
    return this.oidcSecurityService.isAuthenticated('1') ? true : false;
  }

  login(configId: number = 1): void { 
    this.logout(); 
    this.oidcSecurityService.authorize(configId.toString());
    this.afterLoginEvent.emit({ realm: 'LongmanRd', client: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' });
  }

  logout(configId: number = 1): void {
    const cfg = configId.toString();
    if (this.oidcSecurityService.isAuthenticated(cfg)) {
      this.oidcSecurityService.logoffAndRevokeTokens(cfg).subscribe(() => {
        window.location.href = window.location.origin;
        this.afterLogoutEvent.emit({ realm: 'LongmanRd', client: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' });
      });
    }
  }
}
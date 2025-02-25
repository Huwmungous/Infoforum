import { Injectable } from "@angular/core";
import { OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";

@Injectable({ providedIn: 'root' })
export class AuthConfigService {
  private currentConfig: OpenIdConfiguration;

  constructor() { this.currentConfig = buildAuthConfig('0', '', ''); }

  get config() { return this.currentConfig; }

  updateConfig(realm: string, client: string) {
    this.currentConfig = buildAuthConfig('', realm, client);
  }
}

export function buildAuthConfig(configId: string, realm: string, client: string): OpenIdConfiguration {
    return {
        configId: configId ? configId : '0',
        authority: 'https://longmanrd.net/auth/realms/' + (realm ? realm : realmFromName(realm)),
        redirectUrl: window.location.origin + '/auth-callback',
        postLogoutRedirectUri: window.location.origin,
        clientId: client ? client : DEFAULT_CLIENT,
        scope: 'openid profile email offline_access',
        responseType: 'code',
        silentRenew: true,
        silentRenewUrl: window.location.origin + '/silent-renew.html',
        useRefreshToken: true, 
        logLevel: 3,
        postLoginRoute: '/'
    };
  }

  export function realmFromName(name: string): string { 
    return name === 'BreakTackle' ? name : 'LongmanRd'; 
  }
import { Injectable } from "@angular/core";
import { OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_AUTHORITY, DEFAULT_REALM, DEFAULT_CLIENT } from "./client.service";

@Injectable({ providedIn: 'root' })
export class AuthConfigService {
  private currentConfig: OpenIdConfiguration;
  private configs: OpenIdConfiguration[] = [];

  constructor() { this.currentConfig = buildAuthConfig('1', '', ''); }

  get config() { return this.currentConfig; }

  setConfigs(configs: OpenIdConfiguration[]) {
    this.configs = configs;
  }

  selectConfig(realm: string, client: string) {
    const config = this.configs.find(cfg => cfg.authority && cfg.authority.includes(realm) && cfg.clientId === client);
    if (config) {
      this.currentConfig = config;
    } else {
      console.warn(`Config not found for realm: ${realm}, client: ${client}`);
    }
  }
}

export function buildAuthConfig(configId: string, realm: string, client: string): OpenIdConfiguration {
    return {
        configId: configId ? configId : '1',
        authority: DEFAULT_AUTHORITY + (realm ? realm : realmFromName(realm)),
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

  export function realmFromName(name: string): string { return name === 'Default' ? DEFAULT_REALM : name; }
import { Injectable } from "@angular/core";
import { importProvidersFrom } from '@angular/core';
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";

@Injectable({ providedIn: 'root' })
export class AuthConfigService {
  private currentConfig: OpenIdConfiguration;
  private configs: OpenIdConfiguration[] = [];

  constructor() { 
    this.currentConfig = buildAuthConfig('1', '', ''); 
  }

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

export function provideAuth(realm: string = '', client: string = '') {
  return importProvidersFrom(AuthModule.forRoot({ config: buildAuthConfig('1', realmFromName(realm), client) }));
}

export function provideMultipleAuths(configs: OpenIdConfiguration[]) {
  return importProvidersFrom(AuthModule.forRoot({ config: configs }));
}
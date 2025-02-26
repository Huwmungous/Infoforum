import { Injectable } from "@angular/core";
import { importProvidersFrom } from '@angular/core';
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";

@Injectable({ providedIn: 'root' })
export class AuthConfigService {
  
  private _configId: string = '1';
  get configId(): string { return this._configId; }
  set configId(value: string) { this._configId = value; }

  static configs: OpenIdConfiguration[] = [];

  constructor() { }

  selectConfigById(configId: number) {
    console.log(AuthConfigService.configs);
    const config = AuthConfigService.configs.find(c => c.configId === configId.toString());
    if (config) {
      this.configId = config.configId || '1';
    } else {
      console.warn(`Config not found for id: ${configId}`);
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
  var configs = [];
  configs.push(buildAuthConfig('1', realmFromName(realm), client));
  return importProvidersFrom(AuthModule.forRoot({ config: configs }));
}

export function provideMultipleAuths() {
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}
import { importProvidersFrom, Injectable } from "@angular/core"; 
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";

export const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';

@Injectable({ providedIn: 'root' })
export class AuthConfigService {

  private static clients = [
    { id: 1, realmName: 'Default', client: '9F32F055-D2FF-4461-A47B-4A2FCA6720DA' },
    { id: 2, realmName: 'Intelligence', client: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' },
    { id: 3, realmName: 'BreakTackle', client: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
    { id: 4, realmName: 'LongmanRd', client: '9F32F055-D2FF-4461-A47B-4A2FCA6720DA' }
  ];

  static configs: OpenIdConfiguration[] = []; 
  
  static initialise() {
    AuthConfigService.configs = AuthConfigService.clients.map(c => buildAuthConfig(c.id.toString(), c.realmName, c.client));
    console.log("✅ AuthConfigService initialized"); 
  }

  static initialConfig(): OpenIdConfiguration {
    return AuthConfigService.configs[0];
  }
  
  private _configId: string = '1';
  get configId(): string { return this._configId; }
  set configId(value: string) { this._configId = value; }

  constructor() {
    this.loadStoredConfig();
  }

  get clients() {
    return AuthConfigService.clients;
  }

  get configs() {
    return AuthConfigService.configs;
  }

  private loadStoredConfig() {
    const storedConfigId = localStorage.getItem('selectedConfigId') || '1';
    this.selectConfigById(Number(storedConfigId));
  }

  selectConfigById(configId: number) {
    const config = this.configs.find(c => c.configId === configId.toString());
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
    authority: KEYCLOAK_BASE_URL + (realm ? realm : realmFromName(realm)),
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

export function provideConfig(realm: string = '', client: string = '') {
  var configs = [];
  configs.push(buildAuthConfig('1', realmFromName(realm), client));
  return importProvidersFrom(AuthModule.forRoot({ config: configs }));
}

export function provideMultipleConfigs() {
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

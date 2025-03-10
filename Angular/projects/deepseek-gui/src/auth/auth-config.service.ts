import { importProvidersFrom, Injectable } from "@angular/core"; 
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { environment } from "../environments/environment";
import { LogAuthService } from "./log-auth.service";

export const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';

@Injectable({ providedIn: 'root' })
export class AuthConfigService {

  // Static config storage – note this will be cleared on a full reload.
  static configs: OpenIdConfiguration[] = [];   
  private _configId: string = '1';
  get configId(): string { return this._configId; }
  set configId(value: string) { this._configId = value; }

  constructor() {}

  get configs() { return AuthConfigService.configs; }

  async initialize() { this.loadLastConfig(); }

  loadLastConfig() { 
    if (!AuthConfigService.configs.some(c => c.configId === '1')) {
      const newConfig = buildConfig('1', 'LongmanRd', '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7');
      AuthConfigService.configs.push(newConfig);
    }
    this.configId = '1';
  }
}

export function buildConfig(config: string, realm : string, client: string): OpenIdConfiguration {
  const cfg = { 
    configId: config,
    authority: KEYCLOAK_BASE_URL + realm,

    redirectUrl: location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'auth-callback',
    postLogoutRedirectUri: location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'auth-callback',
    
    clientId: client,
    scope: 'openid profile email offline_access',
    responseType: 'code',
    silentRenew: true,
    silentRenewUrl: window.location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'silent-renew.html',
    useRefreshToken: true,
    // Here we assign the actual storage reference rather than a serialized version:
    storage: localStorage,
    
    // Add a unique prefix to avoid conflicts with Keycloak's own storage
    storagePrefix: 'app-auth-' + config + '-',
    logLevel: environment.production ? 0 : 3,
    postLoginRoute: '/',
    
    // Additional settings to improve state handling
    disableRefreshIdTokenAuthTimeValidation: true,
    ignoreNonceAfterRefresh: true,
    
    // Enable secure options
    secureRoutes: [location.origin]
  };
  
  new LogAuthService().logAuthDebug('Config:', cfg);
  return cfg;
}

export function provideConfig(realm: string, clientId: string) {
  // Always rebuild the configuration if none exists
  if (AuthConfigService.configs.length === 0) {
    AuthConfigService.configs.push(buildConfig('1', realm,  clientId));
  }
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

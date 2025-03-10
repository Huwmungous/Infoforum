import { importProvidersFrom, Injectable } from "@angular/core"; 
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";
import { environment } from "../environments/environment";
// import { LogAuthService } from "./log-auth.service"; // if needed

export const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';

@Injectable({ providedIn: 'root' })
export class AuthConfigService {

  // Static config storage – note this will be cleared on a full reload.
  static configs: OpenIdConfiguration[] = []; 

  static initialConfig(): OpenIdConfiguration {
    return AuthConfigService.configs[0];
  }
  
  private _configId: string = '1';
  get configId(): string { return this._configId; }
  set configId(value: string) { this._configId = value; }

  constructor() {
    // Attempt to rehydrate configuration from localStorage on instantiation.
    this.loadLastConfig();
  }

  get configs() {
    return AuthConfigService.configs;
  }

  private loadLastConfig() {
    const storedConfigId = localStorage.getItem('selectedConfigId') || '1';
    // Only build the config if it's not already loaded.
    if (!AuthConfigService.configs.some(c => c.configId === storedConfigId)) {
      AuthConfigService.configs.push(buildConfig(storedConfigId, DEFAULT_CLIENT));
    }
    this.configId = storedConfigId;
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

export function buildConfig(configId: string, clientId: string): OpenIdConfiguration {
  const cfg = { 
    configId: configId || '1',
    authority: KEYCLOAK_BASE_URL + 'LongmanRd',

    redirectUrl: location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'auth-callback',
    postLogoutRedirectUri: location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'auth-callback',
    
    clientId: clientId || DEFAULT_CLIENT,
    scope: 'openid profile email offline_access',
    responseType: 'code',
    silentRenew: true,
    silentRenewUrl: window.location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'silent-renew.html',
    useRefreshToken: true,
    storage: localStorage,
    
    // Add a unique prefix to avoid conflicts with Keycloak's own storage
    storagePrefix: 'app-auth-' + configId + '-',
    logLevel: environment.production ? 0 : 3,
    postLoginRoute: '/',
    
    // Additional settings to improve state handling
    disableRefreshIdTokenAuthTimeValidation: true,
    ignoreNonceAfterRefresh: true,
    
    // Enable secure options
    secureRoutes: [location.origin]
  };
  
  // Optionally log the built configuration for debugging:
  // new LogAuthService().logAuthDebug('buildAuthConfig', cfg);
  return cfg;
}

export function provideConfig(clientId: string) {
  // Ensure the configuration is rehydrated before passing it to the Auth module.
  if (AuthConfigService.configs.length === 0) {
    const storedConfigId = localStorage.getItem('selectedConfigId') || '1';
    AuthConfigService.configs.push(buildConfig(storedConfigId, clientId));
  }
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

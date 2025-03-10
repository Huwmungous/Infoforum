import { importProvidersFrom, Injectable } from "@angular/core"; 
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";
import { environment } from "../environments/environment";

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
    this.loadLastConfig();
  }

  get configs() {
    return AuthConfigService.configs;
  }

  private loadLastConfig() {
    // Read the minimal data from localStorage
    const storedConfigId = localStorage.getItem('selectedConfigId') || '1';
    // Optionally, you could read the clientId if needed:
    const storedClientId = localStorage.getItem('selectedClientId') || DEFAULT_CLIENT;
    
    // Rebuild the config using buildConfig; this will ensure that the storage reference is correctly set
    const newConfig = buildConfig(storedConfigId, storedClientId);
    
    // Ensure we add it only if it's not already there.
    if (!AuthConfigService.configs.some(c => c.configId === newConfig.configId)) {
      AuthConfigService.configs.push(newConfig);
    }
    this.configId = storedConfigId;
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
    // Here we assign the actual storage reference rather than a serialized version:
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
  
  return cfg;
}

export function provideConfig(clientId: string) {
  // Always rebuild the configuration if none exists
  if (AuthConfigService.configs.length === 0) {
    const storedConfigId = localStorage.getItem('selectedConfigId') || '1';
    const storedClientId = localStorage.getItem('selectedClientId') || clientId;
    AuthConfigService.configs.push(buildConfig(storedConfigId, storedClientId));
  }
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

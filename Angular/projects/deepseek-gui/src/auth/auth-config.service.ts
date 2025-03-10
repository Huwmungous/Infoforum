import { importProvidersFrom, Injectable } from "@angular/core"; 
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";
import { environment } from "../environments/environment";
import { LogAuthService } from "./log-auth.service";

export const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';

@Injectable({ providedIn: 'root' })
export class AuthConfigService {

  static configs: OpenIdConfiguration[] = []; 

  static initialConfig(): OpenIdConfiguration {
    return AuthConfigService.configs[0];
  }
  
  private _configId: string = '1';
  get configId(): string { return this._configId; }
  set configId(value: string) { this._configId = value; }

  constructor() {}

  get configs() {
    return AuthConfigService.configs;
  }

  private loadLastConfig() {
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

export function buildConfig(configId: string, clientId: string): OpenIdConfiguration {
  const cfg = { 
    configId: configId ? configId : '1',
    authority: KEYCLOAK_BASE_URL + 'LongmanRd',

    redirectUrl: location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'auth-callback',
    postLogoutRedirectUri: location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'auth-callback',
    
    clientId: clientId ? clientId : DEFAULT_CLIENT,
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
  
  // new LogAuthService().logAuthDebug('buildAuthConfig', cfg);
  return cfg;
}

export function provideConfig(clientId: string) {
  if(AuthConfigService.configs.length === 0)
    AuthConfigService.configs.push(buildConfig('1', clientId));
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

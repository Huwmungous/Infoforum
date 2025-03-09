import { importProvidersFrom, Injectable } from "@angular/core"; 
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";
import { environment } from "../environments/environment";

export const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';

@Injectable({ providedIn: 'root' })
export class AuthConfigService {

  private static clients = [
    { id: 1, realmName: 'Default', client: '9F32F055-D2FF-4461-A47B-4A2FCA6720DA' },
    { id: 2, realmName: 'Intelligence', client: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' },
    { id: 3, realmName: 'BreakTackle', client: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
    { id: 4, realmName: 'LongmanRd', client: '9F32F055-D2FF-4461-A47B-4A2FCA6720DA' }
  ];

  static multipleConfigs: boolean = false;

  static configs: OpenIdConfiguration[] = []; 
  
  static initialiseMultipleConfigs() {
    AuthConfigService.configs = AuthConfigService.clients.map(c => buildConfig(c.id.toString(), c.realmName, c.client)); 
  }

  static initialConfig(): OpenIdConfiguration {
    return AuthConfigService.configs[0];
  }
  
  private _configId: string = '1';
  get configId(): string { return this._configId; }
  set configId(value: string) { this._configId = value; }

  constructor() { 
    if(AuthConfigService.multipleConfigs)
      this.loadLastConfig();
  }

  get clients() {
    return AuthConfigService.clients;
  }

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

export function buildConfig(configId: string, clientName: string, clientId: string): OpenIdConfiguration {
  const cfg = { 
    configId: configId ? configId : '1',
    authority: KEYCLOAK_BASE_URL + (clientName ? clientName : realmFromClientName(clientName)),
    
    // Ensure consistent redirectUrl format with no double slashes
    redirectUrl: location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'auth-callback',
    
    // Match the redirectUrl pattern for post-logout
    postLogoutRedirectUri: location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'auth-callback',
    
    clientId: clientId ? clientId : DEFAULT_CLIENT,
    scope: 'openid profile email offline_access',
    responseType: 'code',
    silentRenew: true,
    
    // Fix the silentRenewUrl to use correct origin and path separator
    silentRenewUrl: window.location.origin + (environment.appName.startsWith('/') ? environment.appName : '/' + environment.appName) + 'silent-renew.html',
    
    useRefreshToken: true,
    
    // Add storage configuration to prevent conflicts
    storage: localStorage,
    
    // Add a unique prefix to avoid conflicts with Keycloak's own storage
    storagePrefix: 'app-auth-' + configId + '-',
    
    // Reduce log level in production
    logLevel: environment.production ? 0 : 3,
    
    postLoginRoute: '/',
    
    // Additional settings to improve state handling
    disableRefreshIdTokenAuthTimeValidation: true,
    ignoreNonceAfterRefresh: true,
    
    // Enable secure options
    secureRoutes: [location.origin]
  };
  
  console.log('buildAuthConfig', cfg);
  return cfg;
}

export function realmFromClientName(name: string): string { 
  return name.toLowerCase() === 'breaktackle' ? name : 'LongmanRd'; 
}

export function provideConfig(clientName: string = '', clientId: string = '') {
  if(AuthConfigService.configs.length === 0)
    AuthConfigService.configs.push(buildConfig('1', realmFromClientName(clientName), clientId));
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

export function provideMultipleConfigs() {
  AuthConfigService.multipleConfigs = true;
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

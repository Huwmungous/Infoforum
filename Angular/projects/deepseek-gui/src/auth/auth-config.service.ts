import { importProvidersFrom, Injectable } from "@angular/core"; 
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";
import { environment } from "../environments/environment";
import { LogAuthService } from "./log-auth.service";

export const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';

@Injectable({ providedIn: 'root' })
export class AuthConfigService {

  // private static clients = [
  //   { id: 1, clientName: 'Default', clientId: '9F32F055-D2FF-4461-A47B-4A2FCA6720DA' },
  //   { id: 2, clientName: 'Intelligence', clientId: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' },
  //   { id: 3, clientName: 'BreakTackle', clientId: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
  //   { id: 4, clientName: 'LongmanRd', clientId: '9F32F055-D2FF-4461-A47B-4A2FCA6720DA' }
  // ];

  // static multipleConfigs: boolean = false;

  static configs: OpenIdConfiguration[] = []; 
  
  // static initialiseMultipleConfigs() {
  //   AuthConfigService.configs = AuthConfigService.clients.map(c => buildConfig(c.id.toString(), c.clientName, c.clientId)); 
  // }

  static initialConfig(): OpenIdConfiguration {
    return AuthConfigService.configs[0];
  }
  
  private _configId: string = '1';
  get configId(): string { return this._configId; }
  set configId(value: string) { this._configId = value; }

  constructor() { 
    // if(AuthConfigService.multipleConfigs)
    //   this.loadLastConfig();
  }

  // get clients() {
  //   return AuthConfigService.clients;
  // }

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

export function provideConfig(clientName: string, clientId: string) {
  if(AuthConfigService.configs.length === 0)
    AuthConfigService.configs.push(buildConfig('1', clientName, clientId));
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

// export function provideMultipleConfigs() {
//   AuthConfigService.multipleConfigs = true;
//   return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
// }

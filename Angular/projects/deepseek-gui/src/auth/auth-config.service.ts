import { importProvidersFrom, Injectable } from "@angular/core"; 
import { AuthModule, OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";
import { environment } from "../environments/environment";
import { LogAuthService } from "./log-auth.service";

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

export function buildConfig(configId: string, realm: string, client: string): OpenIdConfiguration {
  // Normalize the app name path - remove any leading or trailing slashes
  const appPath = environment.appName 
    ? environment.appName.replace(/^\/+|\/+$/g, '')
    : '';
  
  const baseUrl = location.origin;

  
  // const logger = new LogAuthService();
  // logger.logAuthDebug(`Production = ${environment.production}`);
  // logger.logAuthDebug(`Location Hostname = ${location.hostname}`);
  
  let redirectUrl;
  if (environment.production && location.hostname === 'longmanrd.net') {
    const appPath = environment.appName ? `/${environment.appName}` : '';
    redirectUrl = `${baseUrl}${appPath}/auth-callback`;
  } else {
    redirectUrl = `${baseUrl}/auth-callback`;
  }
  
  let silentRenewUrl;
  if (environment.production && location.hostname === 'longmanrd.net') {
    silentRenewUrl = baseUrl + '/intelligence/silent-renew.html';
  } else {
    silentRenewUrl = baseUrl + '/silent-renew.html';
  }

  // logger.logAuthDebug('Building config for realm:', realm);
  // logger.logAuthDebug('client:', client);
  // logger.logAuthDebug('realm:', client);
  // logger.logAuthDebug('baseUrl:', baseUrl);
  // logger.logAuthDebug('appPath:', appPath);
  // logger.logAuthDebug('redirectUrl:', redirectUrl);
  // logger.logAuthDebug('silentRenewUrl:', silentRenewUrl);
  
  const cfg = { 
    configId: configId ? configId : '1',
    authority: KEYCLOAK_BASE_URL + (realm ? realm : realmFromName(realm)),
    redirectUrl: redirectUrl,
    postLogoutRedirectUri: redirectUrl,
    clientId: client ? client : DEFAULT_CLIENT,
    scope: 'openid profile email offline_access',
    responseType: 'code',
    silentRenew: true,
    silentRenewUrl: silentRenewUrl,
    useRefreshToken: true,
    storage: localStorage,
    storagePrefix: 'app-auth-' + configId + '-',
    logLevel: environment.production ? 0 : 3,
    postLoginRoute: '/' + (appPath ? appPath : ''),
    
    // Additional configuration for reliable auth processing
    disablePkce: false,
    tokenRefreshInSeconds: 60,
    renewTimeBeforeTokenExpiresInSeconds: 30,
    triggerAuthorizationResultEvent: true,
    
    // Add these to improve compatibility
    ignoreNonceAfterRefresh: true,
    clearHashAfterLogin: true,
    tokenAcquisitionTimeout: 10000
  };
 
  // logger.logAuthDebug('Config:', cfg);
  return cfg;
}

export function realmFromName(name: string): string { 
  if (name.toLowerCase() === 'intelligence') return 'Intelligence';
  if (name.toLowerCase() === 'breaktackle') return 'BreakTackle';
  return 'LongmanRd'; 
}

export function provideConfig(realm: string = '', client: string = '') {
  if(AuthConfigService.configs.length === 0)
    AuthConfigService.configs.push(buildConfig('1', realmFromName(realm), client));
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

export function provideMultipleConfigs() {
  AuthConfigService.multipleConfigs = true;
  return importProvidersFrom(AuthModule.forRoot({ config: AuthConfigService.configs }));
}

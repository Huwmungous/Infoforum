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
  
  const auth = `${KEYCLOAK_BASE_URL}${(realm ? realm : realmFromName(realm))}`;
  const redirect = `${window.location.origin}/auth-callback'`;
  const clnt = client ? client : DEFAULT_CLIENT;
  const renew = `${window.location.origin}/silent-renew.html`;
  const cfgId = configId ? configId : '1';

  console.log('buildAuthConfig()');
  console.log('id:', cfgId);
  console.log('auth:', auth);
  console.log('redirect:', redirect);
  console.log('client:', clnt);
  console.log('renew:', renew);
  
  const result = {
    configId: cfgId,
    authority: auth,
    redirectUrl: redirect,
    postLogoutRedirectUri: window.location.origin,
    clientId: clnt,
    scope: 'openid profile email offline_access',
    responseType: 'code',
    silentRenew: true,
    silentRenewUrl: renew,
    useRefreshToken: true, 
    logLevel: 3,
    postLoginRoute: 'auth-callback'
  };
  console.log('result:', result); 
  return result;
}

export function realmFromName(name: string): string { 
  return name === 'BreakTackle' ? name : 'LongmanRd'; 
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

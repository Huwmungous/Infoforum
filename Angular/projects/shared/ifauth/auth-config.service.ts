import { Injectable } from "@angular/core"; 
import { OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";

const clients = [
  { id: 1, realmName: 'Default', client: DEFAULT_CLIENT },
  { id: 2, realmName: 'Intelligence', client: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' },
  { id: 3, realmName: 'BreakTackle', client: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
  { id: 4, realmName: 'LongmanRd', client: DEFAULT_CLIENT }
];

export const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';
@Injectable({ providedIn: 'root' })
export class AuthConfigService {

  
  private _configId: string = '1';
  get configId(): string { return this._configId; }
  set configId(value: string) { this._configId = value; }

  private configs: OpenIdConfiguration[] = []; 

  constructor() {
    this.setClients(clients);
    this.loadStoredConfig();
    console.log("✅ AuthConfigService initialized");
  }

  setClients(clients: { id: number, realmName: string, client: string }[]) {
    this.configs = clients.map(c => this.buildAuthConfig(c.id.toString(), c.realmName, c.client));
  }

  get clients() {
    return clients;
  }

  private loadStoredConfig() {
    const storedConfigId = localStorage.getItem('selectedConfigId') || '1';
    this.selectConfigById(Number(storedConfigId));
  }

  buildAuthConfig(configId: string, realm: string, client: string): OpenIdConfiguration {
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

  selectConfigById(configId: number) {
    console.log(this.configs);  // ✅ Now uses instance property
    const config = this.configs.find(c => c.configId === configId.toString());
    if (config) {
      this.configId = config.configId || '1';
    } else {
      console.warn(`Config not found for id: ${configId}`);
    }
  }

  getInitialAuthConfig(): OpenIdConfiguration {
    return this.configs.find(c => c.configId === this._configId) ?? this.configs[0];
  }
}

export function realmFromName(name: string): string { 
  return name === 'BreakTackle' ? name : 'LongmanRd'; 
}

import { Injectable } from "@angular/core"; 
import { OpenIdConfiguration } from "angular-auth-oidc-client";
import { DEFAULT_CLIENT } from "./client.service";

export const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';
@Injectable({ providedIn: 'root' })
export class AuthConfigService {
  
  private _configId: string = '1';
  get configId(): string { return this._configId; }
  set configId(value: string) { this._configId = value; }

  private configs: OpenIdConfiguration[] = []; 

  constructor() {
    console.log("✅ AuthConfigService initialized"); // Debugging output
  }

  setClients(clients: { id: number, realmName: string, client: string }[]) {
    this.configs = clients.map(c => this.buildAuthConfig(c.id.toString(), c.realmName, c.client));
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
}

export function realmFromName(name: string): string { 
  return name === 'BreakTackle' ? name : 'LongmanRd'; 
}

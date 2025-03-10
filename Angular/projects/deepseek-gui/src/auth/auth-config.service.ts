import { Injectable } from "@angular/core"; 
import { OpenIdConfiguration } from "angular-auth-oidc-client";
import { environment } from "../environments/environment";

export const KEYCLOAK_BASE_URL = 'https://longmanrd.net/auth/realms/';

@Injectable({ providedIn: 'root' })
export class AuthConfigService {

  constructor() {}

  static config: OpenIdConfiguration;
  static realm : string;

  get configId() : string { return AuthConfigService.config.configId || '1'; }
  get client() : string { return AuthConfigService.config.clientId || ''; }
  get realm() : string { return AuthConfigService.realm || ''; }

  static buildConfig(config: string, realm : string, client: string): OpenIdConfiguration {
    AuthConfigService.realm = realm;
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
      ignoreNonceAfterRefresh: false,
      
      // Enable secure options
      secureRoutes: [location.origin]
    };
    
    AuthConfigService.config = cfg;
    return cfg;
  }
} 

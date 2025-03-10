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
    
    // Normalize the app path to ensure consistency
    const appPath = environment.appName ? 
      (environment.appName.startsWith('/') ? environment.appName : 
        '/' + environment.appName) : 
        '/';
      
    // Ensure appPath ends with a slash
    const normalizedAppPath = appPath.endsWith('/') ? appPath : appPath + '/';
    
    const cfg = {
      configId: config,
      authority: KEYCLOAK_BASE_URL + realm,
      redirectUrl: location.origin + normalizedAppPath + 'auth-callback',
      postLogoutRedirectUri: appPath,
      clientId: client,
      scope: 'openid profile email offline_access',
      responseType: 'code',
      silentRenew: true,
      silentRenewUrl: location.origin + normalizedAppPath + 'silent-renew.html',
      useRefreshToken: true,
      storage: localStorage,
      storagePrefix: 'app-auth-' + config + '-',
      logLevel: environment.production ? 0 : 3,
      postLoginRoute: '/',
      
      // Enhanced security and state handling
      disableRefreshIdTokenAuthTimeValidation: true,
      ignoreNonceAfterRefresh: false,
      startCheckSession: true,
      
      // Add these to improve state handling
      renewTimeBeforeTokenExpiresInSeconds: 30,
      tokenRefreshInSeconds: 10,
      silentRenewTimeoutInSeconds: 20,
      
      // Cookie handling
      useCookiesForState: true, // Try using cookies for state as a fallback
      
      // CORS settings
      secureRoutes: [environment.apiUrl]
    };
    
    // Store config for later reference
    AuthConfigService.config = cfg;
    
    // Log config in non-production for debugging
    if (!environment.production) {
      console.log('Auth config:', JSON.stringify(cfg, null, 2));
    }
    
    return cfg;
  }
  

} 

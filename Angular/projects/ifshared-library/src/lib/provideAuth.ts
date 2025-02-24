// src/lib/provideAuth.ts
import { importProvidersFrom } from '@angular/core';
import { AuthModule, OpenIdConfiguration, PassedInitialConfig } from 'angular-auth-oidc-client';
import { DEFAULT_CLIENT } from './client.service';

    
export function realmFromName(name: string): string { 
  return name === 'BreakTackle' ? name : 'LongmanRd'; 
}

export function provideAuth(realm: string = '', client: string = '') {
  const cfg : OpenIdConfiguration = {
    authority: 'https://longmanrd.net/auth/realms/' + (realm ? realm : realmFromName(realm)),
    redirectUrl: window.location.origin + '/auth-callback',
    postLogoutRedirectUri: window.location.origin,
    clientId: client ? client : DEFAULT_CLIENT,
    scope: 'openid profile email offline_access',
    responseType: 'code',
    silentRenew: true,
    silentRenewUrl: window.location.origin + '/silent-renew.html',
    useRefreshToken: true, 
    // storage: localStorage, 
    logLevel: 3,
    postLoginRoute: '/'  
    // disablePKCE: false 
  };



  console.log('provideAuth: ', cfg);

  return importProvidersFrom(AuthModule.forRoot({ config: cfg }));
}

// src/lib/provideAuth.ts
import { importProvidersFrom } from '@angular/core';
import { AuthModule, OpenIdConfiguration, PassedInitialConfig } from 'angular-auth-oidc-client';

export function provideAuth(realm: string = '', client: string = '') {
  const cfg : OpenIdConfiguration = {
    authority: 'https://longmanrd.net/auth/realms/' + (realm ? realm : 'LongmanRd'), //default realm,
    redirectUrl: window.location.origin + '/auth-callback',
    postLogoutRedirectUri: window.location.origin,
    clientId: client ? client : '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7', // default clientId,
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

  return importProvidersFrom(AuthModule.forRoot({ config: cfg }));
}

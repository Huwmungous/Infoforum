// src/lib/provideAuth.ts
import { importProvidersFrom } from '@angular/core';
import { AuthModule, OpenIdConfiguration } from 'angular-auth-oidc-client';
import { buildAuthConfig, realmFromName } from './auth-config.service';

export function provideAuth(realm: string = '', client: string = '') {
  return importProvidersFrom(AuthModule.forRoot({ config: buildAuthConfig('0', realmFromName(realm), client) }));
}

export function provideMultipleAuths(configs: OpenIdConfiguration[]) {
  return importProvidersFrom(AuthModule.forRoot({ config: configs }));
}



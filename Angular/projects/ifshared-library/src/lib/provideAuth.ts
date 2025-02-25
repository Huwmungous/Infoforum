// src/lib/provideAuth.ts
import { importProvidersFrom } from '@angular/core';
import { AuthModule } from 'angular-auth-oidc-client';
import { buildAuthConfig, realmFromName } from './auth-config.service';

export function provideAuth(realm: string = '', client: string = '') { 
  const cfg = buildAuthConfig(realmFromName(realm), client); 
  return importProvidersFrom(AuthModule.forRoot({ config: cfg }));
}



// src/lib/provideAuth.ts
import { importProvidersFrom } from '@angular/core';
import { AuthModule } from 'angular-auth-oidc-client';

export function provideAuth(config: any) {
  return importProvidersFrom(AuthModule.forRoot(config));
}

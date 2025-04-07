
// src/app/auth/auth.config.ts
import { AuthModule, LogLevel, OpenIdConfiguration } from 'angular-auth-oidc-client';
import { APP_INITIALIZER, NgModule } from '@angular/core'; 
import { AuthService } from './auth.service';

export const authConfig: OpenIdConfiguration = {
  authority: 'https://your-identity-server.com',
  redirectUrl: window.location.origin,
  postLogoutRedirectUri: window.location.origin,
  clientId: 'your-client-id',
  scope: 'openid profile email api', // Adjust scopes as needed
  responseType: 'code',
  silentRenew: true,
  silentRenewUrl: `${window.location.origin}/silent-renew.html`,
  renewTimeBeforeTokenExpiresInSeconds: 30,
  useRefreshToken: true,
  logLevel: LogLevel.Debug,
};

export function initializeAuth(authService: AuthService) {
  return () => authService.initialize();
}

@NgModule({
  imports: [
    AuthModule.forRoot({
      config: authConfig,
    }),
  ],
  providers: [
    {
      provide: APP_INITIALIZER,
      useFactory: initializeAuth,
      deps: [AuthService],
      multi: true,
    },
  ],
})
export class AuthConfigModule {}
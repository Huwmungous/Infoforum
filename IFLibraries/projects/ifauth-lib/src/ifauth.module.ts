// src/ifauth.module.ts

import { NgModule, ModuleWithProviders } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { LogoutService } from './logout-service';
import { AuthGuard } from './auth.guard';  
import { AuthCallbackComponent } from './components/auth-callback-component/auth-callback.component';

@NgModule({
  imports:[AuthCallbackComponent],
  declarations: [  ],
  exports: [  AuthCallbackComponent ],
  providers: []
})
export class IFAuthModule {
  static forRoot(config: any): ModuleWithProviders<IFAuthModule> {
    return {
      ngModule: IFAuthModule,
      providers: [
        { provide: 'AUTH_CONFIG', useValue: config },
        OidcSecurityService,
        LogoutService,
        AuthGuard
      ]
    };
  }
}

import { NgModule, ModuleWithProviders, EnvironmentProviders } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthConfigService, provideConfig } from './auth-config.service';
import { IFTokenInterceptor } from './token-interceptor';
import { AuthGuard } from './auth.guard';
import { ClientService } from './client.service';
import { AuthModule } from 'angular-auth-oidc-client';

@NgModule({
  imports: [CommonModule, AuthModule],
  exports: []
})
export class IFAuthModule {
  // The module’s constructor is executed as soon as the module is loaded.
  constructor(authConfigService: AuthConfigService) {
    // Immediately rehydrate the configuration.
    authConfigService.loadLastConfig();
  }

  static forRoot(): ModuleWithProviders<IFAuthModule> {
    const configProviders: EnvironmentProviders = provideConfig('Longmanrd', '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7');
    return {
      ngModule: IFAuthModule,
      providers: [
        configProviders,
        { provide: HTTP_INTERCEPTORS, useClass: IFTokenInterceptor, multi: true },
        AuthConfigService,
        AuthGuard,
        ClientService,
        AuthModule
      ]
    };
  }
}

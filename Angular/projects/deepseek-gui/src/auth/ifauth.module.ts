//IFAuthModule.module.ts

import { NgModule, ModuleWithProviders, importProvidersFrom, EnvironmentProviders } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthConfigService, provideConfig, provideMultipleConfigs } from './auth-config.service';
import { IFTokenInterceptor } from './token-interceptor';
import { AuthGuard } from './auth.guard';
import { ClientService } from './client.service';
import { AuthModule } from 'angular-auth-oidc-client';

export interface IFAuthConfigOptions {
  realm: string;
  client: string;
  multiple?: boolean;
}

@NgModule({
  imports: [CommonModule, AuthModule],
  exports: []
})
export class IFAuthModule {
  static forRoot(options: IFAuthConfigOptions): ModuleWithProviders<IFAuthModule> {
    const configProviders: EnvironmentProviders = options.multiple
      ? provideMultipleConfigs()
      : provideConfig(options.realm, options.client);
      
    // Since EnvironmentProviders might not be iterable, we include it as is.
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

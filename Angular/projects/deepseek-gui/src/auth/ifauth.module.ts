//IFAuthModule.module.ts

import { NgModule, ModuleWithProviders, importProvidersFrom, EnvironmentProviders } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthConfigService, provideConfig } from './auth-config.service';
import { IFTokenInterceptor } from './token-interceptor';
import { AuthGuard } from './auth.guard';
import { ClientService } from './client.service';
import { AuthModule } from 'angular-auth-oidc-client';

export interface IFAuthConfigOptions {
  clientName: string;
  clientId: string;
  multiple: false;
}

@NgModule({
  imports: [CommonModule, AuthModule],
  exports: []
})
export class IFAuthModule {
  static forRoot(options: IFAuthConfigOptions): ModuleWithProviders<IFAuthModule> {
    const configProviders: EnvironmentProviders = provideConfig(options.clientName, options.clientId);
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

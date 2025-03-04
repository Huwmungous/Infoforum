import { NgModule, ModuleWithProviders, importProvidersFrom, EnvironmentProviders } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { IFTokenInterceptor } from './ifauth/token-interceptor';
import { AuthConfigService, provideConfig, provideMultipleConfigs } from './ifauth/auth-config.service';
import { AuthGuard } from './ifauth/auth.guard';
import { ClientService } from './ifauth/client.service';
import { AuthModule } from 'angular-auth-oidc-client';

export interface IFAuthConfigOptions {
  realm: string;
  client: string;
  multiple?: boolean;
}

@NgModule({
  imports: [CommonModule],
  exports: [] // Export any shared components, directives, or pipes if needed
})
export class IFAuthModule {
  static forRoot(options: IFAuthConfigOptions): ModuleWithProviders<IFAuthModule> {
    // Choose the appropriate provider function based on options.multiple:
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
        ClientService
      ]
    };
  }
}

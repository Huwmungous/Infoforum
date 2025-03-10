import { NgModule, ModuleWithProviders, EnvironmentProviders } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthConfigService } from './auth-config.service';
import { IFTokenInterceptor } from './token-interceptor';
import { AuthGuard } from './auth.guard';
import { ClientService } from './client.service';
import { AuthModule, provideAuth } from 'angular-auth-oidc-client';

@NgModule({
  imports: [CommonModule, AuthModule],
  exports: []
})
export class IFAuthModule {
  static forRoot(): ModuleWithProviders<IFAuthModule> { 
    return {
      ngModule: IFAuthModule,
      providers: [
        provideAuth({config : AuthConfigService.buildConfig( '1', 'LongmanRd', '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7') }),
        { provide: HTTP_INTERCEPTORS, useClass: IFTokenInterceptor, multi: true },
        AuthGuard,
        ClientService,
        AuthModule
      ]
    };
  }
}

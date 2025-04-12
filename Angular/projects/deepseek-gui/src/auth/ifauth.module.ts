import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthConfigService } from './auth-config.service';
import { IFTokenInterceptor } from './token-interceptor';
import { AuthGuard } from './auth.guard';
import { ClientService } from './client.service';
import { AuthModule, provideAuth } from 'angular-auth-oidc-client'; 

const cfg = AuthConfigService.buildConfig( '1', environment.realm, environment.clientId );
import { environment } from '../environments/environment';

@NgModule({
  imports: [CommonModule, AuthModule],
  providers: [
    provideAuth({config : cfg }),
    { provide: HTTP_INTERCEPTORS, useClass: IFTokenInterceptor, multi: true },
    AuthGuard,
    ClientService,
    AuthConfigService
  ]
})
export class IFAuthModule {}

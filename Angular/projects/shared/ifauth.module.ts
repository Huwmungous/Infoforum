import { CommonModule } from '@angular/common';
import { Component, importProvidersFrom } from '@angular/core';
import { HTTP_INTERCEPTORS, provideHttpClient } from '@angular/common/http';
import { AuthConfigService } from './ifauth/auth-config.service';
import { AuthGuard } from './ifauth/auth.guard';
import { IFTokenInterceptor } from './ifauth/token-interceptor';
import { AuthCallbackComponent } from './ifauth/auth-callback.component';
import { ClientService } from './ifauth/client.service';

@Component({
  standalone: true,
  imports: [CommonModule], 
  selector: 'if-auth',
  template: '<ng-content></ng-content>',
  providers: [
    AuthConfigService,
    AuthGuard,
    ClientService,
    {
      provide: HTTP_INTERCEPTORS,
      useClass: IFTokenInterceptor,
      multi: true
    },
    AuthCallbackComponent
  ]
})
export class IFAuthModule { }

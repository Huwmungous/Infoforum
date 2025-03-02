import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthConfigService } from './ifauth/auth-config.service';
import { AuthGuard } from './ifauth/auth.guard';
import { IFTokenInterceptor } from './ifauth/token-interceptor';
import { AuthCallbackComponent } from './ifauth/auth-callback.component';
import { ClientService } from './ifauth/client.service';   

@NgModule({
  imports: [
    CommonModule,
    AuthCallbackComponent
  ],
  providers: [
    AuthConfigService,
    AuthGuard,
    ClientService,  
    {
      provide: HTTP_INTERCEPTORS,
      useClass: IFTokenInterceptor,
      multi: true   
    }
  ],
  exports: [
    AuthCallbackComponent
  ]
})
export class IFAuthModule { }

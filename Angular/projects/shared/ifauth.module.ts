import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthConfigService } from './ifauth/auth-config.service';
import { AuthGuard } from './ifauth/auth.guard';
import { IFTokenInterceptor } from './ifauth/token-interceptor';
import { AuthCallbackComponent } from './ifauth/auth-callback.component';

@NgModule({
  imports: [
    CommonModule,
    AuthCallbackComponent
  ],
  providers: [
    AuthConfigService,
    AuthGuard,
    IFTokenInterceptor
  ],
  exports: [
    AuthCallbackComponent
  ]
})
export class IfauthModule { }
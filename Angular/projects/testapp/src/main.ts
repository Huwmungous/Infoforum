import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { importProvidersFrom, inject } from '@angular/core';
import { provideAuth, OpenIdConfiguration } from 'angular-auth-oidc-client';
import { AuthCallbackComponent } from '../../shared/ifauth/auth-callback.component';
import { AuthGuard } from '../../shared/ifauth/auth.guard';
import { AuthConfigService } from '../../shared/ifauth/auth-config.service';
import { IFAuthModule } from '../../shared/ifauth.module';
import { IFTokenInterceptor } from '../../shared/ifauth/token-interceptor';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: AppComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

AuthConfigService.initialise();

bootstrapApplication(AppComponent, {
  providers: [
    AuthConfigService,
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi()),
    provideAuth({ config: AuthConfigService.initialConfig() }), 
    {
      provide: HTTP_INTERCEPTORS, 
      useClass: IFTokenInterceptor,
      multi: true
    },
    importProvidersFrom(IFAuthModule)
  ]
})
  .then(appRef => console.log("✅ Angular bootstrapped successfully"))
  .catch(err => console.error("❌ Bootstrap error:", err));

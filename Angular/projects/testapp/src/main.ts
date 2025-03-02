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

const authConfigService = new AuthConfigService();
const initialConfig = authConfigService.getInitialAuthConfig();

// ✅ Get auth config dynamically using a factory function
const authConfigFactory = (): OpenIdConfiguration => {
  const authConfigService = inject(AuthConfigService); // Inject service manually
  return authConfigService.buildAuthConfig('1', 'LongmanRd', '9F32F055-D2FF-4461-A47B-4A2FCA6720DA'); 
};

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: AppComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptorsFromDi()), // ✅ Enables HttpClient with DI-based interceptors
    provideAuth({ config: initialConfig }), // ✅ Dynamically provide auth config
    {
      provide: HTTP_INTERCEPTORS, // ✅ Register interceptor at the application level
      useClass: IFTokenInterceptor,
      multi: true
    },
    importProvidersFrom(IFAuthModule), // ✅ Import IFAuthModule
    provideRouter(routes),
    AuthConfigService // ✅ Ensure the service is available
  ]
})
  .then(appRef => console.log("✅ Angular bootstrapped successfully"))
  .catch(err => console.error("❌ Bootstrap error:", err));

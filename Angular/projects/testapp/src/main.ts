import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router'; 
import { AuthCallbackComponent } from '../../shared/ifauth/auth-callback.component';
import { AuthGuard } from '../../shared/ifauth/auth.guard';
import { AuthConfigService } from '../../shared/ifauth/auth-config.service'; 
import { IFTokenInterceptor } from '../../shared/ifauth/token-interceptor';
import { ClientService } from '../../shared/ifauth/client.service';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: AppComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptorsFromDi()), // ✅ Moved provideHttpClient() here
    {
      provide: HTTP_INTERCEPTORS, // ✅ Register interceptor at the application level
      useClass: IFTokenInterceptor,
      multi: true
    }, 
    provideRouter(routes),
    AuthConfigService, // ✅ Explicitly provided
    AuthGuard, // ✅ Explicitly provided
    ClientService // ✅ Explicitly provided
  ]
})
.then(appRef => {
  const injector = appRef.injector;
  try {
    console.log("Available providers:", injector);
    const authConfigService = injector.get(AuthConfigService);
    console.log("AuthConfigService resolved successfully:", authConfigService);
  } catch (error) {
    console.error("Service resolution error:", error);
  }
})
.catch(err => console.error(err));

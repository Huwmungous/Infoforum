import { bootstrapApplication } from '@angular/platform-browser';
import { provideAnimations } from '@angular/platform-browser/animations';
import { AppComponent } from './app/app.component';
import { HTTP_INTERCEPTORS, provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { AuthCallbackComponent, AuthGuard, IFTokenInterceptor, provideMultipleConfigs } from 'ifauth-lib';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: AppComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

bootstrapApplication(AppComponent, {
  providers: [
    { provide: HTTP_INTERCEPTORS, useClass: IFTokenInterceptor, multi: true },
    provideMultipleConfigs(),
    provideHttpClient(),
    provideRouter(routes),
    provideAnimations()
  ]
})
.catch(err => console.error(err));
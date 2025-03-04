import { bootstrapApplication } from '@angular/platform-browser';
import { provideAnimations } from '@angular/platform-browser/animations';
import { AppComponent } from './app/app.component';
import { HTTP_INTERCEPTORS, provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { IntelligenceComponent } from './app/deepseek/intelligence.component';
import { AuthCallbackComponent, AuthGuard, IFTokenInterceptor, provideConfig } from 'ifauth-lib';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

bootstrapApplication(AppComponent, {
  providers: [
    { provide: HTTP_INTERCEPTORS, useClass: IFTokenInterceptor, multi: true },
    provideConfig('LongmanRd', '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7'),
    provideHttpClient(),
    provideRouter(routes),
    provideAnimations()
  ]
})
.catch(err => console.error(err));
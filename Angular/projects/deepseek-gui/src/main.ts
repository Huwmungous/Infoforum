import { bootstrapApplication } from '@angular/platform-browser';
import { provideAnimations } from '@angular/platform-browser/animations';
import { AppComponent } from './app/app.component';
import { HTTP_INTERCEPTORS, provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { IFTokenInterceptor } from '../../shared/ifauth/token-interceptor';
import { provideAuth } from '../../shared/ifauth/auth-config.service'
import { AuthGuard } from '../../shared/ifauth/auth.guard';
import { AuthCallbackComponent } from '../../shared/ifauth/auth-callback.component';
import { IntelligenceComponent } from './app/deepseek/intelligence.component';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

bootstrapApplication(AppComponent, {
  providers: [
    { provide: HTTP_INTERCEPTORS, useClass: IFTokenInterceptor, multi: true },
    provideAuth('LongmanRd', '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7'),
    provideHttpClient(),
    provideRouter(routes),
    provideAnimations()
  ]
})
.catch(err => console.error(err));
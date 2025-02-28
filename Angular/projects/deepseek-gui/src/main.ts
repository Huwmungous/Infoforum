import { bootstrapApplication } from '@angular/platform-browser';
import { provideAnimations } from '@angular/platform-browser/animations';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { TokenInterceptor, provideAuth, AuthGuard, AuthCallbackComponent } from 'ifshared-library';
import { IntelligenceComponent } from './app/deepseek/intelligence.component';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

bootstrapApplication(AppComponent, {
  providers: [
    provideAuth('LongmanRd', '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7'),
    provideHttpClient(),
    provideRouter(routes),
    provideAnimations(),
    { provide: TokenInterceptor, useClass: TokenInterceptor }
  ]
})
.catch(err => console.error(err));
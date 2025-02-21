import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Routes } from '@angular/router'; 
import { IntelligenceComponent } from './app/deepseek/intelligence.component';
import { authConfig } from './app/auth.config';
import { importProvidersFrom } from '@angular/core';
import { provideAuth, AuthGuard, AuthCallbackComponent } from 'ifauth-lib';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' }, 
  { path: '**', redirectTo: '/home' } 
];

bootstrapApplication(AppComponent, {
  providers: [
    provideAuth(authConfig),
    provideHttpClient(),
    provideAnimations(),
    provideRouter(routes)
  ]
})
.catch(err => console.error(err));
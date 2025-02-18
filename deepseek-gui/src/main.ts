import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Routes } from '@angular/router'; 
import { IntelligenceComponent } from './app/deepseek/intelligence.component';
import { AuthModule  } from 'angular-auth-oidc-client';
import { authConfig } from './app/auth.config';
import { importProvidersFrom } from '@angular/core';
import { AuthGuard } from './app/auth.guard';
import { AuthCallbackComponent } from './app/components/auth-callback-component/auth-callback-component.component';

const routes: Routes = [
  { path: 'callback', component: AuthCallbackComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' }, // Default route
  { path: '**', redirectTo: '/home' } // Wildcard route
];

bootstrapApplication(AppComponent, {
  providers: [
    importProvidersFrom(
      AuthModule.forRoot({
        config: authConfig
      })
    ),
    provideHttpClient(),
    provideAnimations(),
    provideRouter(routes)
  ]
})
.catch(err => console.error(err));
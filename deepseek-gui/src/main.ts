import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { importProvidersFrom } from '@angular/core';
import { OAuthModule } from 'angular-oauth2-oidc';
import { provideRouter, Routes } from '@angular/router'; 
import { AuthGuard } from './app/auth.guard';
import { IntelligenceComponent } from './app/deepseek/intelligence.component';
import { AuthGuard } from './app/auth.guard';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' }, // Default route
  { path: '**', redirectTo: '/home' } // Wildcard route
];

bootstrapApplication(AppComponent, {
  providers: [
    importProvidersFrom(
      OAuthModule.forRoot({
        resourceServer: {
          sendAccessToken: true,
        },
      })
    ),
    provideHttpClient(),
    provideAnimations(),
    provideRouter(routes)
  ]
})
.catch(err => console.error(err));
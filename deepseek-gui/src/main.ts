import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { importProvidersFrom } from '@angular/core';
import { OAuthModule } from 'angular-oauth2-oidc';
import { provideRouter, Routes } from '@angular/router';
import { LoginComponent } from './app/components/login/login.component';
import { AuthGuard } from './app/components/login/auth.guard';
import { IntelligenceComponent } from './app/deepseek/intelligence.component';

const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'intelligence', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/intelligence', pathMatch: 'full' }, // Default route
  { path: '**', redirectTo: '/intelligence' } // Wildcard route
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
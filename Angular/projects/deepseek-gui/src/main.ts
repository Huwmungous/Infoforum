import { bootstrapApplication } from '@angular/platform-browser';
import { provideAnimations } from '@angular/platform-browser/animations';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { provideAuth, AuthGuard, AuthCallbackComponent } from 'ifshared-library';
import { IntelligenceComponent } from './app/deepseek/intelligence.component';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

bootstrapApplication(AppComponent, {
  providers: [
    provideAuth(),
    provideHttpClient(),
    provideRouter(routes),
    provideAnimations()
  ]
})
.catch(err => console.error(err));
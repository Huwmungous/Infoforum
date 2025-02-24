import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { provideAuth, AuthGuard, AuthCallbackComponent } from 'ifshared-library';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: AppComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

// Retrieve saved client values from local storage
const savedClientId = localStorage.getItem('selectedClientClientId') || '';
const savedClientName = localStorage.getItem('selectedClientName') || '';

console.log('Saved client: ', savedClientName, savedClientId);

// debugger;

bootstrapApplication(AppComponent, {
  providers: [
    provideAuth(savedClientName, savedClientId),
    provideHttpClient(),
    provideRouter(routes)
  ]
})
.catch(err => console.error(err));
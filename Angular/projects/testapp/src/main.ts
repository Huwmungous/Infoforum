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

const savedRealm = localStorage.getItem('selectedClientName') || '';
const savedClient = localStorage.getItem('selectedClientClientId') || '';

console.log('Saved client: ', savedRealm, savedClient);

debugger;

bootstrapApplication(AppComponent, {
  providers: [
    provideAuth(savedRealm, savedClient),
    provideHttpClient(),
    provideRouter(routes)
  ]
})
.catch(err => console.error(err));
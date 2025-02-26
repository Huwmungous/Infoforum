import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { provideMultipleAuths, AuthGuard, AuthCallbackComponent, DEFAULT_CLIENT, DEFAULT_REALM, buildAuthConfig, realmFromName } from 'ifshared-library';

export const clients = [
  { id: 1, realmName: 'Default', client: DEFAULT_CLIENT },
  { id: 2, realmName: 'Intelligence', client: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' },
  { id: 3, realmName: 'BreakTackle', client: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
  { id: 4, realmName: 'LongmanRd', client: DEFAULT_CLIENT }
];

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: AppComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

// Use consistent keys ("realm" and "client") that your AppComponent uses.
const savedRealm = localStorage.getItem('realm') || 'Default';
const savedClient = localStorage.getItem('client') || DEFAULT_CLIENT;

console.log('Bootstrapping with realm:', savedRealm, 'client:', savedClient);

bootstrapApplication(AppComponent, {
  providers: [
    provideMultipleAuths(configsFromClients(clients)),
    provideHttpClient(),
    provideRouter(routes)
  ]
})
.catch(err => console.error(err));

function configsFromClients(clients: { id: number; realmName: string; client: string; }[]) {
  return clients.map(client => buildAuthConfig(client.id.toString(), realmFromName(client.realmName), client.client));
}


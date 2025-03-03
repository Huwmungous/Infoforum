import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { DEFAULT_CLIENT } from '../../shared/ifauth/client.service';
import { AuthCallbackComponent } from '../../shared/ifauth/auth-callback.component';
import { AuthGuard } from '../../shared/ifauth/auth.guard';
import { AuthConfigService, buildAuthConfig, realmFromName, provideMultipleConfigs } from '../../shared/ifauth/auth-config.service';

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

bootstrapApplication(AppComponent, {
  providers: [
    provideMultipleConfigs(),
    AuthConfigService,
    provideHttpClient(),
    provideRouter(routes)
  ]
})
.then(appRef => {
  const injector = appRef.injector;
  const authConfigService = injector.get(AuthConfigService);
})
.catch(err => console.error(err));
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { DEFAULT_CLIENT } from '../../shared/ifauth/client.service';
import { AuthCallbackComponent } from '../../shared/ifauth/auth-callback.component';
import { AuthGuard } from '../../shared/ifauth/auth.guard';
import { AuthConfigService } from '../../shared/ifauth/auth-config.service';
import { ClientService } from '../../shared/ifauth/client.service';
import { importProvidersFrom } from '@angular/core';
import { IFAuthModule } from 'shared/ifauth.module';

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
    importProvidersFrom(IFAuthModule), // ✅ Correct standalone import
    provideHttpClient(), // ✅ Moved here to avoid DI errors
    provideRouter(routes)
  ]
})
.then(appRef => {
  const injector = appRef.injector;
  try {
    const authConfigService = injector.get(AuthConfigService);
    authConfigService.setClients(clients);
    console.log("AuthConfigService resolved successfully:", authConfigService);
  } catch (error) {
    console.error("Service resolution error:", error);
  }
})
.catch(err => console.error(err));

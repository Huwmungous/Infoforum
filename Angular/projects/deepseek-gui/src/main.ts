import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { importProvidersFrom, Injector } from '@angular/core';
import { IFAuthModule } from './auth/ifauth.module';
import { AuthCallbackComponent } from './auth/auth-callback.component';
import { IntelligenceComponent } from './app/deepseek/intelligence.component';
import { AuthGuard } from './auth/auth.guard';
import { SilentRenewComponent } from './auth/silent-renew.component';
import { AuthConfigService } from './auth/auth-config.service';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'silent-renew', component: SilentRenewComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
]; 

bootstrapApplication(AppComponent, {
  providers: [
    importProvidersFrom(IFAuthModule),
    provideHttpClient(),
    provideRouter(routes)
  ]
}).catch(err => console.error(err));


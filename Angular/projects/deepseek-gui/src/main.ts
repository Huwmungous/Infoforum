import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { importProvidersFrom, Injector } from '@angular/core';
import { IFAuthModule } from './auth/ifauth.module';
import { AuthConfigService } from './auth/auth-config.service';
import { AuthCallbackComponent } from './auth/auth-callback.component';
import { IntelligenceComponent } from './app/deepseek/intelligence.component';
import { AuthGuard } from './auth/auth.guard';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

const bootstrapApp = async () => {
  const injector = Injector.create({
    providers: [
      { provide: AuthConfigService, useClass: AuthConfigService, deps: [] }
    ]
  });
  
  // Get the AuthConfigService instance and wait for its initialization
  const authConfigService = injector.get(AuthConfigService);
  await authConfigService.initialize();

  // Now that configuration is rehydrated, bootstrap the app
  bootstrapApplication(AppComponent, {
    providers: [
      importProvidersFrom(IFAuthModule.forRoot()),
      provideHttpClient(),
      provideRouter(routes)
    ]
  }).catch(err => console.error(err));
};

bootstrapApp();

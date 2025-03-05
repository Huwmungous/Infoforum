import { bootstrapApplication } from '@angular/platform-browser';
import { provideAnimations } from '@angular/platform-browser/animations';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { importProvidersFrom } from '@angular/core';
import { IntelligenceComponent } from './app/deepseek/intelligence.component';
import { IFAuthModule, AuthCallbackComponent, AuthGuard } from '../../../dist/ifauth-lib';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: IntelligenceComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

bootstrapApplication(AppComponent, {
  providers: [
    importProvidersFrom(IFAuthModule.forRoot({
      realm: 'LongmanRd',
      client: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7',
      multiple: false // or true if you want to support multiple configs
    })),
    provideHttpClient(),
    provideRouter(routes),
    provideAnimations()
  ]
}).catch(err => console.error(err));

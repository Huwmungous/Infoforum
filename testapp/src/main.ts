import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, Routes } from '@angular/router';
import { provideAuth, AuthGuard, AuthCallbackComponent } from 'ifauth-lib';

const routes: Routes = [
  { path: 'auth-callback', component: AuthCallbackComponent },
  { path: 'home', component: AppComponent, canActivate: [AuthGuard] },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
  { path: '**', redirectTo: '/home' }
];

bootstrapApplication(AppComponent, {
  providers: [
    provideAuth('BreakTackle', '46279F81-ED75-4CFA-868C-A36AE8BE22B0'),
    provideHttpClient(),
    provideRouter(routes)
  ]
})
.catch(err => console.error(err));
import { Routes } from '@angular/router';
import { AppComponent } from './app.component'; 
import { AuthCallbackComponent } from './core/auth/auth-callback.component';
import { SilentRenewComponent } from './core/auth/silent-renew.component';
import { AuthGuard } from './core/auth/auth.guard';

export const routes: Routes = [
    { path: 'auth-callback', component: AuthCallbackComponent },
    { path: 'silent-renew', component: SilentRenewComponent },
    {
        path: 'home',
        component: AppComponent,
        canActivate: [AuthGuard]
    },
    { path: '', redirectTo: '/home', pathMatch: 'full' },
    { path: '**', redirectTo: '/home' }
];



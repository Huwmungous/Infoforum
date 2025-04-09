import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AuthModule, OidcSecurityService } from 'angular-auth-oidc-client';
import { CommonModule } from '@angular/common';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-auth-callback',
  template: `
  <div> *** AUTH CALLBACK COMPONENT *** </div>`,
  styleUrls: ['./auth-callback.component.scss'],
  standalone: true,
  imports: [CommonModule, AuthModule]
})
export class AuthCallbackComponent implements OnInit {
  debugMode = !environment.production;
  debugInfo: any = {};
  loading: boolean = true;
  message = 'Authenticating...';

  constructor(
    private oidc: OidcSecurityService,
    private route: ActivatedRoute
  ) {}

  ngOnInit() {
    this.loading = true;    
    this.oidc.checkAuth().subscribe({
      next: (authResult) => {
        console.log('Auth Callback - Auth Result:', authResult);
        this.loading = false;
      },
      error: (err) => {
        console.error('Auth Callback - Error during checkAuth():', err);
        this.message = err.message || 'Authentication error';
        this.loading = false;
      }
    });
  }


}
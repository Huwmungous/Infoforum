import { Component, Inject, OnInit } from '@angular/core';
import { AuthModule, OidcSecurityService } from 'angular-auth-oidc-client';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthConfigService } from './auth-config.service';

@Component({
    selector: 'app-auth-callback',
    templateUrl: './auth-callback.component.html',
    styleUrls: ['./auth-callback.component.scss'],
    standalone: true,
    imports: [ CommonModule, AuthModule ]
})

export class AuthCallbackComponent implements OnInit {
  constructor(
    private oidcSecurityService: OidcSecurityService,
    private authConfigService: AuthConfigService,
    private router: Router
  ) {}

  ngOnInit() {
    console.log('Auth Callback - checkAuth()');
    console.log(this.authConfigService.configs);
    console.log('Config ID : ', this.authConfigService.configId);
    this.oidcSecurityService.checkAuth().subscribe({
      next: ({ isAuthenticated }) => {
        if (isAuthenticated) {
          this.router.navigate(['/']);
        } else {
          console.error('Auth Callback - Authentication failed');
        }
      },
      error: (error) => console.error('Auth Callback - Error during checkAuth():', error)
    });
  }
}

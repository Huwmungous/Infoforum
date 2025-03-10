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
    
    // Check if there are error parameters in the URL
    const urlParams = new URLSearchParams(window.location.search);
    const error = urlParams.get('error');
    const errorDescription = urlParams.get('error_description');
    
    if (error) {
      console.error('Auth Callback - Error in URL parameters:', error, errorDescription);
    }
    
    this.oidcSecurityService.checkAuth().subscribe({
      next: ({ isAuthenticated, userData, accessToken, idToken }) => {        
        if (isAuthenticated) {
          this.router.navigate(['/']);
        } else {
          console.error('Auth Callback - Authentication failed');
          
          // Try to get more information about why authentication failed
          this.oidcSecurityService.getAuthenticationResult().subscribe({
            next: (result) => console.log('Auth result:', result),
            error: (err) => console.error('Error getting auth result:', err)
          });
        }
      },
      error: (error) => {
        console.error('Auth Callback - Error during checkAuth():', error);
        
        // Additional error details
        if (error && error.message) {
          console.error('Error message:', error.message);
        }
        
        if (error && error.stack) {
          console.error('Error stack:', error.stack);
        }
      }
    });
  }
}
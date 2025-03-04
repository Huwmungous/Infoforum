import { Component, Inject, OnInit } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-auth-callback',
    templateUrl: './auth-callback.component.html',
    styleUrls: ['./auth-callback.component.scss'],
    standalone: true,
    imports: [ CommonModule ]
})

export class AuthCallbackComponent implements OnInit {
  constructor(
    private oidcSecurityService: OidcSecurityService,
    @Inject(Router) private router: Router
  ) {}

  ngOnInit() {
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

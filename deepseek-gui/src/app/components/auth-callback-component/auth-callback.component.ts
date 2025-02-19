import { Component, OnInit } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Router } from '@angular/router';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

@Component({
  selector: 'app-auth-callback',
  templateUrl: './auth-callback.component.html',
  styleUrls: ['./auth-callback.component.scss'],
  standalone: true,
  imports: [MatProgressSpinnerModule]
})
export class AuthCallbackComponent implements OnInit {
  constructor(
    private oidcSecurityService: OidcSecurityService,
    private router: Router
  ) {}

  ngOnInit() {
    this.oidcSecurityService.checkAuth().subscribe({
      next: ({ isAuthenticated }) => {
        console.log('Auth Callback - Authenticated:', isAuthenticated);
        if (isAuthenticated) {
          this.router.navigate(['/']);
        } else {
          console.error('Auth Callback - Authentication failed');
          // Optionally, navigate to an error page.
        }
      },
      error: (error) => console.error('Auth Callback - Error during checkAuth():', error)
    });
  }
}

import { Component, OnInit } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Router, RouterModule } from '@angular/router';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  standalone: true,
  imports: [RouterModule]
})
export class AppComponent implements OnInit {
  private tokenRefreshed = false; // Prevent multiple token refreshes

  constructor(
    private oidcSecurityService: OidcSecurityService,
    private router: Router
  ) {}

  ngOnInit() {
    this.oidcSecurityService.checkAuth().subscribe(({ isAuthenticated, idToken }) => {
      console.log('AppComponent: Authenticated?', isAuthenticated);

      if (isAuthenticated) {
        console.log('AppComponent: User is authenticated.');
        if (!this.tokenRefreshed) {
          this.tokenRefreshed = true;
          this.refreshToken();
        }
      } else {
        console.log('AppComponent: Not authenticated, waiting for AuthGuard.');
      }
    });
  }

  private refreshToken() {
    this.oidcSecurityService.forceRefreshSession().subscribe({
      next: (result) => console.log('Token refreshed:', result),
      error: (error) => console.error('Error refreshing token:', error),
    });
  }

  logout() {
    this.oidcSecurityService.getIdToken().subscribe(idToken => {
      if (idToken) {
        const logoutUrl = `https://longmanrd.net/auth/realms/LongmanRd/protocol/openid-connect/logout?id_token_hint=${idToken}&post_logout_redirect_uri=${encodeURIComponent(window.location.origin)}`;
        window.location.href = logoutUrl;  // Redirect to logout URL
      } else {
        console.error('Logout error: No ID Token found');
      }
    });
  }
}

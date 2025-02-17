import { Component, OnInit } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { authConfig } from './auth.config';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    RouterModule
  ]
})
export class AppComponent implements OnInit {
  constructor(
    private oidcSecurityService: OidcSecurityService,
    private router: Router) {}

  ngOnInit() {
    // this.oidcSecurityService.configure(authConfig); // Removed as configure method does not exist
    this.oidcSecurityService.checkAuth().subscribe(({ isAuthenticated }) => {
      if (isAuthenticated) {
        this.router.navigate(['/home']);
      } else {
        this.oidcSecurityService.authorize();
      }
    });
  }
}
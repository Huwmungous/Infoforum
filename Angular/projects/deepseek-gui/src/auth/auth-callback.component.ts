import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AuthModule, OidcSecurityService } from 'angular-auth-oidc-client';
import { environment } from '../environments/environment';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-auth-callback',
  templateUrl: './auth-callback.component.html',
  styleUrls: ['./auth-callback.component.scss'],
  standalone: true,
  imports: [ CommonModule, AuthModule ]
})

export class AuthCallbackComponent implements OnInit {
  debugMode = !environment.production;
  debugInfo: any = {};
  error: string = '';

  constructor(
    private oidcSecurityService: OidcSecurityService,
    private route: ActivatedRoute
  ) {}

  ngOnInit() {
    // Collect debug info
    this.debugInfo = {
      url: window.location.href,
      hasState: this.route.snapshot.queryParams['state'] ? true : false,
      queryParams: this.route.snapshot.queryParams,
      localStorage: this.getLocalStorageAuthItems(),
      sessionStorage: this.getSessionStorageAuthItems()
    };
    
    // Let the library handle the callback
    this.oidcSecurityService.checkAuth().subscribe({
      next: (authResult) => {},
      error: (err) => {
        console.error('Auth Callback - Error during checkAuth():', err);
        this.error = err.message || 'Authentication error';
      }
    });
  }

  private getLocalStorageAuthItems() {
    const items: Record<string, string> = {};
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (key && key.includes('auth')) {
        items[key] = localStorage.getItem(key) || '';
      }
    }
    return items;
  }

  private getSessionStorageAuthItems() {
    const items: Record<string, string> = {};
    for (let i = 0; i < sessionStorage.length; i++) {
      const key = sessionStorage.key(i);
      if (key && key.includes('auth')) {
        items[key] = sessionStorage.getItem(key) || '';
      }
    }
    return items;
  }
}
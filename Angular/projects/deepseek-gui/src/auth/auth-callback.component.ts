import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AuthModule, OidcSecurityService } from 'angular-auth-oidc-client';
import { environment } from '../environments/environment';
import { CommonModule } from '@angular/common';
import { LoadingSpinnerComponent } from '../app/components/loading-spinner/loading-spinner.component';
import { delay } from 'rxjs';

@Component({
  selector: 'app-auth-callback',
  template: `
  <div *ngIf="loading" class="spinner-container">
    <p class="msg">{{ message }}</p>
    <app-loading-spinner></app-loading-spinner> 
  </div>`,
  styleUrls: ['./auth-callback.component.scss'],
  standalone: true,
  imports: [CommonModule, AuthModule, LoadingSpinnerComponent]
})
export class AuthCallbackComponent implements OnInit {
  debugMode = !environment.production;
  debugInfo: any = {};
  loading: boolean = true;
  message = 'Authenticating...';

  constructor(
    private oidcSecurityService: OidcSecurityService,
    private route: ActivatedRoute
  ) {}

  ngOnInit() {
    this.loading = true;

    this.debugInfo = {
      url: window.location.href,
      hasState: this.route.snapshot.queryParams['state'] ? true : false,
      queryParams: this.route.snapshot.queryParams,
      localStorage: this.getLocalStorageAuthItems(),
      sessionStorage: this.getSessionStorageAuthItems()
    };
    
    this.oidcSecurityService.checkAuth().subscribe({
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
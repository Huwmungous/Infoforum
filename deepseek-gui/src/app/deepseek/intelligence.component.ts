import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common'; 
import { MatTabsModule } from '@angular/material/tabs';
import { CodeGenComponent } from '../components/code-gen/code-gen.component';
import { OAuthService, OAuthModule, AuthConfig } from 'angular-oauth2-oidc';
import { authConfig } from '../auth-config';

@Component({
  selector: 'app-intelligence',
  templateUrl: './intelligence.component.html',
  styleUrls: ['./intelligence.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    MatTabsModule,
    CodeGenComponent,
    OAuthModule
  ]
})
export class IntelligenceComponent implements OnInit {

  constructor(private oauthService: OAuthService) {
    this.configureWithNewConfigApi();
  }

  private configureWithNewConfigApi() {
    this.oauthService.configure(authConfig);
    this.oauthService.loadDiscoveryDocumentAndTryLogin();
  }

  ngOnInit() {
    this.oauthService.setupAutomaticSilentRefresh();
  }

  login() {
    this.oauthService.initCodeFlow();
  }

  logout() {
    this.oauthService.logOut();
  }

  get name() {
    const claims = this.oauthService.getIdentityClaims();
    return !claims ? null : claims['name'];
  }
}
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common'; 
import { MatTabsModule } from '@angular/material/tabs';
import { CodeGenComponent, generateGUID } from '../components/code-gen/code-gen.component';
import { OAuthService } from 'angular-oauth2-oidc';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { authConfig } from '../auth.config';

@Component({
  selector: 'app-intelligence',
  templateUrl: './intelligence.component.html',
  styleUrls: ['./intelligence.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    MatTabsModule,
    MatIconModule,
    MatButtonModule,
    CodeGenComponent
  ]
})
export class IntelligenceComponent {
  conversationId: string = generateGUID(); // Initialize with a new GUID

  constructor( ) { }

  createNewConversation() {
    this.conversationId = generateGUID(); // Generate a new GUID for the conversationId
  }
}
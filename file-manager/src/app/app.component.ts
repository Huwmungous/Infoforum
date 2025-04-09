// src/app/app.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenavModule } from '@angular/material/sidenav';
import { FileTreeComponent } from './file-manager/components/file-tree/file-tree.component'; 
import { AuthClientService } from './core/auth/auth-client.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatSidenavModule,
    FileTreeComponent
  ],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  title = 'File Manager';
  
  constructor(private authService: AuthClientService) {
  }
  
  ngOnInit(): void {
    // Auth is handled by APP_INITIALIZER in auth.config.ts
  }
  
  login(): void {
    this.authService.login();
  }
  
  logout(): void {
    this.authService.logout();
  }
}
import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common'; 
import { MatTabsModule } from '@angular/material/tabs';
import { CodeGenComponent, generateGUID } from '../components/code-gen/code-gen.component';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { ClientService } from '../../../../shared/ifauth/client.service';

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
  
  conversationId: string = generateGUID(); 

  constructor( @Inject(ClientService) private clientService : ClientService ) { }

  createNewConversation() {
    this.conversationId = generateGUID(); 
  }

  logout() {
    this.clientService.logout();  
  }
  
}
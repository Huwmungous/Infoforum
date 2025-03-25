import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common'; 
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { CodeGenComponent, generateGUID } from '../components/code-gen/code-gen.component'; 
import { ClientService } from '../../auth/client.service';
import { IFAuthModule } from '../../auth/ifauth.module';
import { ChatComponent } from '../components/chat/chat-component';

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
    IFAuthModule,
    CodeGenComponent,
    ChatComponent
  ]
})
export class IntelligenceComponent {
  
  conversationId: string = generateGUID(); 

  constructor(private clientService : ClientService) {}

  createNewConversation() {
    this.conversationId = generateGUID(); 
  }

  logout() {
    this.clientService.logout();  
  }
}
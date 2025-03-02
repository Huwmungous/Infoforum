import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ClientService, DEFAULT_CLIENT } from '../../../shared/ifauth/client.service';
import { AuthConfigService } from '../../../shared/ifauth/auth-config.service';


const clients = [
  { id: 1, realmName: 'Default', client: DEFAULT_CLIENT },
  { id: 2, realmName: 'Intelligence', client: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' },
  { id: 3, realmName: 'BreakTackle', client: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
  { id: 4, realmName: 'LongmanRd', client: DEFAULT_CLIENT }
];

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  providers: []
})
export class AppComponent implements OnInit, OnDestroy {
 
  constructor(
    private clientService: ClientService,
    private authConfigService: AuthConfigService
  ) 
  { 
    console.log("ClientService instance:", clientService);
    console.log("AuthConfigService instance:", authConfigService);
  }

  private afterLoginSubscription!: Subscription;
  private afterLogoutSubscription!: Subscription;

  title = 'Auth Testing';

  get clients() { return clients; }

  get isAuthenticated(): boolean {
    return this.clientService.isAuthenticated();
  }

  get selectedId(): number { 
    return parseInt(localStorage.getItem('selectedClientId') || '1'); 
  }
  set selectedId(value: number) {
    if(value !== parseInt(this.authConfigService.configId)) {
      localStorage.setItem('selectedClientId', value.toString());
      this.authConfigService.configId = value.toString();
    }
  } 

  ngOnInit() {
    this.authConfigService.selectConfigById(this.selectedId);
    
    this.afterLogoutSubscription = this.clientService.afterLogoutEvent.subscribe({
      next: ({ realm, client }: { realm: string, client: string }): void => {
        console.log(`User has logged out from realm: ${realm}.${client}`);
      },
      error: (err: any): void => {
        console.error('Error in afterLogoutEvent:', err);
      }
    });

    this.afterLoginSubscription = this.clientService.afterLoginEvent.subscribe({
      next: ({ realm, client }: { realm: string, client: string }): void => {
        console.log(`User has logged in to realm: ${realm}.${client}`);
      },
      error: (err: any): void => {
        console.error('Error in afterLogoutEvent:', err);
      }
    });
  }

  onClientChange(event: Event) {
    const elem = event.target as HTMLSelectElement;
    const selection = clients.find(client => client.id === +elem.value);
    if (selection) {
      const prev = this.selectedId; 
      this.selectedId = selection.id;
      this.authConfigService.selectConfigById(this.selectedId);

      this.logout(prev);
    }
  }

  logoutCurrent() {
    console.log(`Logging out current client (${this.selectedId})`);
    debugger;
    this.logout(this.selectedId); 
  }

  logout(configId: number = 1) {
    this.clientService.logout(configId);
  }

  ngOnDestroy() {
    if (this.afterLoginSubscription) { this.afterLoginSubscription.unsubscribe(); }
    if (this.afterLogoutSubscription) { this.afterLogoutSubscription.unsubscribe(); }
  }
}
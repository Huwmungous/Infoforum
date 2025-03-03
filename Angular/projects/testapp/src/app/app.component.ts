import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { AuthConfigService, ClientService } from 'ifauth-lib';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  providers: []
})
export class AppComponent implements OnInit, OnDestroy {
 
  constructor(  private clientService: ClientService, private authConfigService: AuthConfigService  )  {}

  private afterLoginSubscription!: Subscription;
  private afterLogoutSubscription!: Subscription;

  title = 'Auth Testing';

  get clients() { return this.authConfigService.clients; }

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
    const selection = this.clients.find(client => client.id === +elem.value);
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
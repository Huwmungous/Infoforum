import { Component, OnInit, OnDestroy } from '@angular/core';
import { AuthConfigService, ClientService, DEFAULT_CLIENT, DEFAULT_REALM } from 'ifshared-library';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { realmFromName } from 'ifshared-library';
import { Subscription } from 'rxjs';
import { clients } from 'src/main';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {

  private loginSubscription!: Subscription;
  title = 'Auth Testing';

  get clients() { return clients; }

  // Using consistent keys for local storage.
  get selectedId(): number { 
    return parseInt(localStorage.getItem('selectedClientId') || '0', 10); 
  }
  set selectedId(value: number) { 
    localStorage.setItem('selectedClientId', value.toString());
  } 

  get selectedName(): string { 
    return localStorage.getItem('selectedClientName') || 'Default'; 
  }
  set selectedName(value: string) { 
    localStorage.setItem('selectedClientName', value);
  }

  get selectedRealm(): string { 
    return localStorage.getItem('realm') || DEFAULT_REALM; 
  }
  set selectedRealm(value: string) { 
    localStorage.setItem('realm', value);
  } 

  get selectedClient(): string { 
    return localStorage.getItem('client') || DEFAULT_CLIENT; 
  }
  set selectedClient(value: string) { 
    localStorage.setItem('client', value);
  }

  constructor(
    private clientService: ClientService,
    private authConfigService: AuthConfigService
  ) { }

  ngOnInit() {
    // At startup, update both services with the saved config.
    this.clientService.setClient(this.selectedRealm, this.selectedClient);
    this.authConfigService.updateConfig(this.selectedRealm, this.selectedClient);
  }

  onClientChange(event: Event) {
    const elem = event.target as HTMLSelectElement;
    const selection = clients.find(client => client.id === +elem.value);
    if (selection) {
      // Save the new selection using consistent keys.
      this.selectedId = selection.id;
      this.selectedName = selection.realmName;
      // Optionally, transform the displayed realm name to an actual realm value.
      this.selectedRealm = realmFromName(selection.realmName);
      this.selectedClient = selection.client;

      // Update both the client service and the auth configuration.
      this.clientService.setClient(this.selectedRealm, this.selectedClient);
      this.authConfigService.updateConfig(this.selectedRealm, this.selectedClient);

      // Trigger a login to reinitialize authentication with the new settings.
      this.login();
    }
  }

  get isAuthenticated(): boolean {
    return this.clientService.isAuthenticated();
  }

  login() {
    this.clientService.login();
  }

  logout() {
    this.clientService.logout();
  }

  ngOnDestroy() {
    if (this.loginSubscription) {
      this.loginSubscription.unsubscribe();
    }
  }
}

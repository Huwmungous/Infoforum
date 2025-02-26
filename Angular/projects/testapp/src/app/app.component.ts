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

  get isAuthenticated(): boolean {
    return this.clientService.isAuthenticated();
  }

  get selectedId(): number { 
    return parseInt(localStorage.getItem('selectedClientId') || '1'); 
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
    this.clientService.setClient(this.selectedRealm, this.selectedClient);
    this.authConfigService.selectConfig(this.selectedRealm, this.selectedClient);
  }

  onClientChange(event: Event) {
    const elem = event.target as HTMLSelectElement;
    const selection = clients.find(client => client.id === +elem.value);
    debugger;
    if (selection) {
      const prev = this.selectedId; 
      this.selectedId = selection.id;
      this.selectedName = selection.realmName; 
      this.selectedRealm = realmFromName(selection.realmName);
      this.selectedClient = selection.client;
 
      this.clientService.setClient(this.selectedRealm, this.selectedClient);
      this.authConfigService.selectConfig(this.selectedRealm, this.selectedClient);

      this.logout(prev);
    }
  }

  login(configId: number = 0) {
    const clientConfig = clients.find(client => client.id === configId);
    if (clientConfig) {
      console.log('LOGIN ConfigId : ', configId, 'Selected client:"', clientConfig.realmName, '" realm: "', realmFromName(clientConfig.realmName), '" client: "', clientConfig.client, '"');
    } else {
      console.log('LOGIN ConfigId : ', configId, 'Selected client not found');
    } 
    this.clientService.login(configId);
  }

  logout(configId: number = 0) {
    const clientConfig = clients.find(client => client.id === configId);
    if (clientConfig) {
      console.log('LOGOUT ConfigId : ', configId, 'Selected client:"', clientConfig.realmName, '" realm: "', realmFromName(clientConfig.realmName), '" client: "', clientConfig.client, '"');
    } else {
      console.log('LOGOUT ConfigId : ', configId, 'Selected client not found');
    } 
    this.clientService.logout(configId);
  }

  ngOnDestroy() {
    if (this.loginSubscription) {
      this.loginSubscription.unsubscribe();
    }
  }
}

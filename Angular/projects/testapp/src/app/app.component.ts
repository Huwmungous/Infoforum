import { Component, OnInit, OnDestroy } from '@angular/core';
import { ClientService, DEFAULT_CLIENT } from '../../../ifshared-library/src/lib/client.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { realmFromName } from '../../../ifshared-library/src/lib/provideAuth';
import { Subscription } from 'rxjs';
import { DEFAULT_REALM } from 'ifshared-library';

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

  clients = [
    { id: 1, realmName: 'Default', client: DEFAULT_CLIENT },
    { id: 2, realmName: 'Intelligence', client: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' },
    { id: 3, realmName: 'BreakTackle', client: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
    { id: 4, realmName: 'LongmanRd', client: DEFAULT_CLIENT }
  ];

  get selectedId(): number { return parseInt(localStorage.getItem('selectedId') || '0', 10); }
  set selectedId(value: number) { localStorage.setItem('selectedId', value.toString())} 

  get selectedName(): string { return localStorage.getItem('realmName') || 'Default'; }
  set selectedName(value: string) { localStorage.setItem('realmName', value) }

  get selectedRealm(): string { return localStorage.getItem('realm') || DEFAULT_REALM; }
  set selectedRealm(value: string) { localStorage.setItem('realm', value) } 

  get selectedClient(): string { return localStorage.getItem('client') || DEFAULT_CLIENT; }
  set selectedClient(value: string) { localStorage.setItem('client', value) }

  loggedInRealm: string = '';
  loggedInClient: string = '';

  constructor(private clientService: ClientService) { }

  ngOnInit() {
    this.clientService.setClient(this.selectedRealm, this.selectedClient);
  }

  onClientChange(event: Event) {
    const elem = event.target as HTMLSelectElement;
    const selection = this.clients.find(client => client.id === +elem.value);
    if (selection) {
      this.selectedId = selection.id; 
      this.selectedName = selection.realmName; 
      this.selectedRealm = realmFromName(selection.realmName); 
      this.selectedClient = selection.client;
      this.clientService.setClient(this.selectedRealm, this.selectedClient);
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
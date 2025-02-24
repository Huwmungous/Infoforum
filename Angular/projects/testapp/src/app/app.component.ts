import { Component, OnInit } from '@angular/core'; 
import { ClientService, DEFAULT_CLIENT } from '../../../ifshared-library/src/lib/client.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  title = 'Auth Testing';

  clients = [
    { id: 1, name: 'Default', clientId: DEFAULT_CLIENT  },
    { id: 2, name: 'Intelligence', clientId: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' },
    { id: 3, name: 'BreakTackle', clientId: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
    { id: 4, name: 'LongmanRd', clientId: DEFAULT_CLIENT }
  ];

  dropdownId: number | null = null;
  selectedClientName: string = '';
  selectedClientId: string = '';

  constructor(private clientService: ClientService) { }

  ngOnInit() {
    const realm = localStorage.getItem('selectedRealm');
    const client = localStorage.getItem('selectedClientClientId'); 
    if (realm && client) {
      this.dropdownId = +client;
      this.selectedClientName = realm;
      const selectedClient = this.clients.find(client => client.id === this.dropdownId);
      if (selectedClient) {
        this.clientService.setClient(realm, client);
      }
    }
  }

  onClientChange(event: Event) {
    const selectElement = event.target as HTMLSelectElement;
    const selectedClient = this.clients.find(client => client.id === +selectElement.value);
    if (selectedClient && this.dropdownId !== selectedClient.id) {
      this.dropdownId = selectedClient.id;
      this.selectedClientName = selectedClient.name;
      this.selectedClientId = selectedClient.clientId;
      localStorage.setItem('selectedClient', this.dropdownId.toString());
      localStorage.setItem('selectedRealm', this.realmFromName(selectedClient.name));
      localStorage.setItem('selectedClientClientId', selectedClient.clientId);
      this.clientService.setClient(selectedClient.name, selectedClient.clientId);
    }
  }
  realmFromName(name: string): string { 
    return name === 'BreakTackle' ? name : 'LongmanRd'; 
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
}
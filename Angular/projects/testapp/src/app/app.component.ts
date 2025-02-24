import { Component, OnInit } from '@angular/core';
import { LogoutService } from 'ifshared-library';
import { ClientService, DEFAULT_CLIENT_ID, DEFAULT_CLIENT_NAME } from '../client.service';
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
    { id: 1, name: 'Default', clientId: DEFAULT_CLIENT_NAME },
    { id: 2, name: 'BreakTackle', clientId: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
    { id: 3, name: 'LongmanRd', clientId: DEFAULT_CLIENT_ID }
  ];

  dropdownId: number | null = null;
  selectedClientName: string = '';

  constructor(private logoutService: LogoutService, private clientService: ClientService) { }

  ngOnInit() {
    const savedClientId = localStorage.getItem('selectedClientId');
    const savedClientName = localStorage.getItem('selectedClientName');
    if (savedClientId && savedClientName) {
      this.dropdownId = +savedClientId;
      this.selectedClientName = savedClientName;
      const selectedClient = this.clients.find(client => client.id === this.dropdownId);
      if (selectedClient) {
        this.clientService.setClient(selectedClient.name, selectedClient.clientId); // Pass name and clientId
      }
    }
  }

  onClientChange(event: Event) {
    const selectElement = event.target as HTMLSelectElement;
    const selectedClient = this.clients.find(client => client.id === +selectElement.value);
    if (selectedClient) {
      this.dropdownId = selectedClient.id;
      this.selectedClientName = selectedClient.name;
      localStorage.setItem('selectedClientId', this.dropdownId.toString());
      localStorage.setItem('selectedClientName', selectedClient.name);
      localStorage.setItem('selectedClientClientId', selectedClient.clientId);
      this.clientService.setClient(selectedClient.name, selectedClient.clientId); // Pass name and clientId
    }
  }

  logout() {
    this.clientService.reinitializeAuth();
    this.logoutService.logout();
  }
}
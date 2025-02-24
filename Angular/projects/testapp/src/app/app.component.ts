import { Component, OnInit } from '@angular/core';
import { LogoutService } from 'ifshared-library';
import { ClientService } from '../client.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  title = 'Auth Testing';

  clients = [
    { id: 1, name: 'Default', clientId: '' },
    { id: 2, name: 'BreakTackle', clientId: '46279F81-ED75-4CFA-868C-A36AE8BE22B0' },
    { id: 3, name: 'LongmanRd', clientId: '53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7' }
  ];

  selectedClientId: number | null = null;

  constructor(private logoutService: LogoutService, private clientService: ClientService) { }

  ngOnInit() {
    const savedClientId = localStorage.getItem('selectedClientId');
    if (savedClientId) {
      this.selectedClientId = +savedClientId;
      const selectedClient = this.clients.find(client => client.id === this.selectedClientId);
      if (selectedClient) {
        this.clientService.setClient(selectedClient.name, selectedClient.clientId); // Pass name and clientId
      }
    }
  }

  onClientChange(event: Event) {
    const selectElement = event.target as HTMLSelectElement;
    const selectedClient = this.clients.find(client => client.id === +selectElement.value);
    if (selectedClient) {
      this.selectedClientId = selectedClient.id;
      localStorage.setItem('selectedClientId', this.selectedClientId.toString());
      localStorage.setItem('selectedClientName', selectedClient.name);
      this.clientService.setClient(selectedClient.name, selectedClient.clientId); // Pass name and clientId
    }
  }

  logout() {
    this.logoutService.logout();
  }
}
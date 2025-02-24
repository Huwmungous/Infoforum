import { Injectable } from '@angular/core';
import { provideAuth } from 'ifshared-library';

@Injectable({
  providedIn: 'root'
})
export class ClientService {
  private clientName: string = '';
  private clientId: string = '';

  setClient(clientName: string, clientId: string) {
    this.clientName = clientName;
    this.clientId = clientId;
  }

  reinitializeAuth() {
    provideAuth(this.clientName, this.clientId);
  }
}
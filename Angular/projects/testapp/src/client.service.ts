import { Injectable } from '@angular/core';
import { provideAuth } from 'ifshared-library';

export const DEFAULT_CLIENT_NAME = 'LongmanRd';
export const DEFAULT_CLIENT_ID = '9F32F055-D2FF-4461-A47B-4A2FCA6720DA';

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
    const name = this.clientName == 'Default' ? DEFAULT_CLIENT_NAME : this.clientName;
    const id = this.clientName == 'Default' ? DEFAULT_CLIENT_ID : this.clientId;
    provideAuth(name, id);
  }
}
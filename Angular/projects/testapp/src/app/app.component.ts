import { Component, OnInit, OnDestroy } from '@angular/core';
import { AuthConfigService, ClientService } from 'ifshared-library';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
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
 
  constructor(
    private clientService: ClientService,
    private authConfigService: AuthConfigService
  ) { }


  private loginSubscription!: Subscription;
  private logoutSubscription!: Subscription;

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

  logoutCurrent( ) {
    console.log(`Logging out current client (${this.selectedId})`);
    debugger;
    this.logout(this.selectedId); 
  }

  logout(configId: number = 1) {
    this.clientService.logout(configId);
  }

  ngOnDestroy() {
    if (this.loginSubscription) { this.loginSubscription.unsubscribe(); }
    if (this.logoutSubscription) { this.logoutSubscription.unsubscribe(); }
  }
}

import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { AuthConfigService } from './ifauth/auth-config.service';
import { AuthGuard } from './ifauth/auth.guard';
import { ClientService } from './ifauth/client.service';

@Component({
  standalone: true,
  imports: [CommonModule],
  template: '<ng-content></ng-content>',
  providers: [
    AuthConfigService,
    AuthGuard,
    ClientService
  ]
})
export class IFAuthModule {}

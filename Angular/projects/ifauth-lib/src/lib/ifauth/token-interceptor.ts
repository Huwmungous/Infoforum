import { Injectable } from '@angular/core';
import {
  HttpEvent,
  HttpRequest,
  HttpHandler,
  HttpInterceptor,
} from '@angular/common/http';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { KEYCLOAK_BASE_URL } from './auth-config.service';

@Injectable({
  providedIn: 'root',
})
export class IFTokenInterceptor implements HttpInterceptor {

  constructor(private oidcSecurityService: OidcSecurityService) {}

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    if (!request.url.includes(KEYCLOAK_BASE_URL)) { 
      return this.oidcSecurityService.getAccessToken().pipe(
        switchMap((token: string) => {
          const clone = request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }); 
          return next.handle(clone);
        })
      );
    }
    return next.handle(request);
  }
}

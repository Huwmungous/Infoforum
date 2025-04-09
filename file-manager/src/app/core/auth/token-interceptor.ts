
import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable, switchMap } from 'rxjs';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable()
export class IFTokenInterceptor implements HttpInterceptor {

  constructor(private oidc: OidcSecurityService) { }

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return this.oidc.getAccessToken().pipe(
      switchMap((token: string) => {
          return token ? 
            next.handle(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })) : 
            next.handle(request); 
      })
    );
  }

}

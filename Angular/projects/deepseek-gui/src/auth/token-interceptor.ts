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

@Injectable({
  providedIn: 'root',
})
export class IFTokenInterceptor implements HttpInterceptor {

  constructor(private oidc: OidcSecurityService) {}

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return this.oidc.getAccessToken().pipe(
      switchMap((token: string) => {
        const clone = request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }); 
        return next.handle(clone);
      })
    );
  }
}

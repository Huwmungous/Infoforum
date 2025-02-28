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

  constructor(private oidcSecurityService: OidcSecurityService) {}

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    console.log('TokenInterceptor: checking request', request.url); 

    if (request.url.includes('http://localhost:5008/IFOllama')) {
      // Use switchMap to wait for the token
      return this.oidcSecurityService.getIdToken().pipe(
        switchMap((token: string) => {
          const clone = request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
          console.log('TokenInterceptor: handling request', clone.url, clone.headers);
          return next.handle(clone);
        })
      );
    }

    console.log('TokenInterceptor: Ignoring request', request.url); 
    return next.handle(request);
  }
}

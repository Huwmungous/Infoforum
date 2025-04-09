
import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable, switchMap } from 'rxjs';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable()
export class IFTokenInterceptor implements HttpInterceptor {

  constructor(private oidc: OidcSecurityService) { 
    console.log('IFTokenInterceptor constructor called');
    this.oidc.isAuthenticated().subscribe(
      auth => console.log('Auth status:', auth)
    );
  }

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    console.log('IFTokenInterceptor - intercept called');
    
    return this.oidc.getAccessToken().pipe(
      switchMap((token: string) => {
        console.log('IFTokenInterceptor - token retrieved:', token ? 'Token exists' : 'No token');
        const clone = request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });  
        return next.handle(clone);
      })
    );
  }

}

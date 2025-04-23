import { Injectable } from '@angular/core';
import {
  HttpEvent,
  HttpRequest,
  HttpHandler,
  HttpInterceptor,
} from '@angular/common/http';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, of } from 'rxjs';
import { switchMap, catchError } from 'rxjs/operators';

@Injectable({
  providedIn: 'root',
})
export class IFTokenInterceptor implements HttpInterceptor {

  constructor(private oidc: OidcSecurityService) {}

  intercept(request: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    return this.oidc.getAccessToken().pipe(
      switchMap((token: string) => {
        // debugger;
        if (token) {
          const clone = request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }); 
          return next.handle(clone);
        } else {
          // console.warn('No access token available');
          return next.handle(request);
        }
      }),
      catchError((error) => {
        console.error('Error in token interceptor:', error);
        return of(error);
      })
    );
  }
}

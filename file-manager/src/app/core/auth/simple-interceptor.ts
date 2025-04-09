import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable()
export class SimpleInterceptor implements HttpInterceptor {
  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    console.log('SimpleInterceptor - Intercept method called');
    const clonedRequest = req.clone({
      setHeaders: {
        Authorization: `Bearer test-token`,
      },
    });
    return next.handle(clonedRequest);
  }
}
import { OidcSecurityService } from "angular-auth-oidc-client";
import { map, Observable } from "rxjs";

export class AuthService {
    constructor(private oidcSecurityService: OidcSecurityService) {}
  
    initialize(): Promise<any> {
      return this.oidcSecurityService.checkAuth().toPromise();
    }
  
    login(): void {
      this.oidcSecurityService.authorize();
    }
  
    logout(): void {
      this.oidcSecurityService.logoff();
    }
  
    get isAuthenticated$(): Observable<boolean> {
      return this.oidcSecurityService.isAuthenticated$.pipe(
        map(authResult => authResult.isAuthenticated)
      );
    }
  
    get token$(): Observable<string> {
      return this.oidcSecurityService.getAccessToken();
    }
  
    get userData$(): Observable<any> {
      return this.oidcSecurityService.userData$;
    }
  
    get username$(): Observable<string> {
      return this.userData$.pipe(
        map(userData => userData?.preferred_username || userData?.name || 'Unknown User')
      );
    }
  }
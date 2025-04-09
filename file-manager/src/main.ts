// src/main.ts
import { bootstrapApplication } from '@angular/platform-browser'; 
import { AppComponent } from './app/app.component';
import { importProvidersFrom } from '@angular/core'; 
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { routes } from './app/app.routes'; 
import { IFAuthModule } from './app/core/auth/ifauth.module';

bootstrapApplication(AppComponent, {
  providers: [ 
    importProvidersFrom(IFAuthModule),
    provideHttpClient(),
    provideRouter(routes)
  ]
}).catch(err => console.error(err));
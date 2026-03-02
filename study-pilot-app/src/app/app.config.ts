import { ApplicationConfig, provideZoneChangeDetection, ErrorHandler } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/http/auth.interceptor';
import { loadingInterceptor } from './core/http/loading.interceptor';
import { apiResponseInterceptor } from './core/http/api-response.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';
import { APP_ENVIRONMENT } from './core/config/environment.token';
import { environment } from '../environments/environment';
import { GlobalErrorHandlerService } from './core/services/global-error-handler.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([authInterceptor, loadingInterceptor, apiResponseInterceptor, errorInterceptor])
    ),
    { provide: APP_ENVIRONMENT, useValue: environment },
    { provide: ErrorHandler, useClass: GlobalErrorHandlerService }
  ]
};

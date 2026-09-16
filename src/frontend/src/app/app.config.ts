import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { apiInterceptor } from '@core/http/api-interceptor';
import { errorInterceptor } from '@core/http/error-interceptor';

/**
 * The only place providers are declared.
 *
 * Interceptor order is behaviour: `apiInterceptor` is outermost so every request has its host
 * resolved before anything else looks at it, and `errorInterceptor` sits closest to the backend so
 * it is the first to see a failure. The reference's auth and refresh interceptors are absent
 * because ComposeLab has no authentication yet.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch(), withInterceptors([apiInterceptor, errorInterceptor])),
  ],
};

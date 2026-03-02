import { InjectionToken } from '@angular/core';

export interface AppEnvironment {
  apiBaseUrl: string;
}

export const APP_ENVIRONMENT = new InjectionToken<AppEnvironment>('APP_ENVIRONMENT');

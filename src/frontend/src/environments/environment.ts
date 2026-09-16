/**
 * Production configuration. Environment files ship to the browser: no secrets, ever.
 *
 * Only `core/` reads this. Features get the base URL through `apiInterceptor`, so no feature
 * knows which environment it is running in and no service hardcodes a host.
 */
export const environment = {
  production: true,
  apiBaseUrl: '',
} as const;

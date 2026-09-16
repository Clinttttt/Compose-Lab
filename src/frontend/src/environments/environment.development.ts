/**
 * Development configuration.
 *
 * The HTTP origin from the API's `launchSettings.json`. HTTP rather than the HTTPS profile on
 * `:7117` so a browser client needs no dev-certificate trust step, and because the API's
 * development CORS allowlist is `http://localhost:4200`.
 */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5131',
} as const;

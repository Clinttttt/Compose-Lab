import { HttpInterceptorFn } from '@angular/common/http';
import { environment } from '@env/environment';

/**
 * The only file that knows the host. Services call `/api/...` and this prepends the base URL, so
 * switching environments touches one file.
 *
 * Deliberate deviation from the reference: it also sets `withCredentials: true`, for an API that
 * pairs with a refresh-token slice and an httpOnly cookie. ComposeLab has no authentication, and
 * its CORS policy intentionally omits `AllowCredentials` — a browser given `withCredentials` and no
 * `Access-Control-Allow-Credentials` in the reply discards the response, so setting it here would
 * fail every cross-origin call. Turning it on requires the backend to allow credentials against an
 * explicit origin in the same change.
 */
export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  // Absolute URLs pass through untouched.
  if (!request.url.startsWith('/')) {
    return next(request);
  }

  return next(request.clone({ url: `${environment.apiBaseUrl}${request.url}` }));
};

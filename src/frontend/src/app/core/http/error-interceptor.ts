import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { Notifier } from '@core/notifications/notifier';

/**
 * The one place an HTTP failure becomes a message.
 *
 * `inject()` is captured in the function body: calling it inside `catchError` throws NG0203. The
 * error is always rethrown, so a caller is never left waiting on a request that quietly vanished.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifier = inject(Notifier);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        notifier.error(toMessage(error));
      }

      return throwError(() => error);
    }),
  );
};

function toMessage(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'Cannot reach the ComposeLab API. Is it running on the configured address?';
  }

  // RFC 7807 Problem Details, as the API's ResultExtensions and exception handler return.
  const problem = error.error as { title?: string; detail?: string } | null;

  return problem?.detail ?? problem?.title ?? 'The request could not be completed.';
}

import { Injectable } from '@angular/core';
import {
  HttpInterceptor, HttpRequest,
  HttpHandler, HttpEvent, HttpErrorResponse
} from '@angular/common/http';
import { EMPTY, Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {

  /**
   * Module-level latch so multiple parallel requests that all 401 at once
   * (e.g. a dashboard's forkJoin firing 6 requests) only trigger a single
   * logout + redirect instead of stacking up.
   */
  private static logoutInFlight = false;

  constructor(private auth: AuthService, private router: Router) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const token = this.auth.getToken();
    const cloned = token
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

    return next.handle(cloned).pipe(
      catchError((err: HttpErrorResponse) => {
        if (err.status === 401) {
          // Token expired/invalid — clear session and redirect to login.
          // Use the static latch so 6 parallel forkJoin requests don't all
          // call logout() and race the router.
          if (!AuthInterceptor.logoutInFlight) {
            AuthInterceptor.logoutInFlight = true;
            this.auth.logout();             // clears storage + navigates to /auth/login
            // Reset the latch on next microtask once the navigation has dispatched
            queueMicrotask(() => { AuthInterceptor.logoutInFlight = false; });
          }
          // Swallow the 401 — the calling component shouldn't try to render
          // an error from a request whose user is being logged out anyway.
          return EMPTY;
        }
        return throwError(() => err);
      })
    );
  }
}

import { HttpBackend, HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, defer, finalize, map, Observable, of, retry, shareReplay, throwError, timer } from 'rxjs';
import { environment } from '../../../environments/environment';

interface AccessTokenResponse {
  accessToken: string;
  expiresAt: string;
}

/**
 * Anonymous guest identity, no signup or login.
 *
 * - Access token (15 min): kept ONLY in this service's memory. Never written to localStorage, so a malicious
 *   script can't read it from storage, and it disappears when the tab closes.
 * - Refresh token (30 days): an HttpOnly cookie set by the API. JavaScript can't read it; the browser sends it
 *   automatically, and only to /api/auth/token.
 *
 * Opening the app (or a new tab) calls /api/auth/token: the cookie proves "same guest as before", the server
 * rotates it and returns a new access token.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  /** Plain HttpClient that bypasses our interceptors (the token call must not wait for a token). */
  private readonly http = new HttpClient(inject(HttpBackend));

  private accessToken: string | null = null;
  private expiresAtMs = 0;

  /** One refresh at a time: concurrent requests all wait for the same call. */
  private inFlight: Observable<string> | null = null;

  /** A valid access token, refreshing first if there is none or it's about to expire. */
  getToken(): Observable<string> {
    const stillValid = this.accessToken && Date.now() < this.expiresAtMs - 30_000;
    return stillValid ? of(this.accessToken!) : this.refresh();
  }

  /** Forces a new access token (used after a 401). */
  refresh(): Observable<string> {
    this.inFlight ??= defer(() =>
      this.http.post<AccessTokenResponse>(environment.apiUrl + 'auth/token', {}, { withCredentials: true }),
    ).pipe(
      // 409 = another tab rotated the cookie a moment ago; the browser now holds the new one, so just retry.
      retry({
        count: 2,
        delay: (err: HttpErrorResponse) => (err.status === 409 ? timer(300) : throwError(() => err)),
      }),
      map((res) => {
        this.accessToken = res.accessToken;
        this.expiresAtMs = Date.parse(res.expiresAt);
        return res.accessToken;
      }),
      catchError((err) => {
        this.accessToken = null;
        return throwError(() => err);
      }),
      finalize(() => (this.inFlight = null)),
      shareReplay(1),
    );
    return this.inFlight;
  }
}

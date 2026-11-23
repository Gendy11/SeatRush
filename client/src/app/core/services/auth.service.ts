import { HttpBackend, HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, defer, finalize, map, Observable, of, retry, shareReplay, throwError, timer } from 'rxjs';
import { environment } from '../../../environments/environment';

interface AccessTokenResponse {
  accessToken: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = new HttpClient(inject(HttpBackend));

  private accessToken: string | null = null;
  private expiresAtMs = 0;

  private inFlight: Observable<string> | null = null;

  getToken(): Observable<string> {
    const stillValid = this.accessToken && Date.now() < this.expiresAtMs - 30_000;
    return stillValid ? of(this.accessToken!) : this.refresh();
  }

  refresh(): Observable<string> {
    this.inFlight ??= defer(() =>
      this.http.post<AccessTokenResponse>(environment.apiUrl + 'auth/token', {}, { withCredentials: true }),
    ).pipe(
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

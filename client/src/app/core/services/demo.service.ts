import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface RushStatus {
  running: boolean;
  mode: 'Normal' | 'Extreme' | null;
  bots: number;
  activeBots: number;
  seatsBooked: number;
  startedAt: string | null;

  queuePosition: number | null;
}

@Injectable({ providedIn: 'root' })
export class DemoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  private url(eventId: number, path: string): string {
    return `${this.baseUrl}demo/events/${eventId}/${path}`;
  }

  startRush(eventId: number): Observable<RushStatus> {
    return this.http.post<RushStatus>(this.url(eventId, 'rush'), {});
  }

  getRush(eventId: number): Observable<RushStatus> {
    return this.http.get<RushStatus>(this.url(eventId, 'rush'));
  }

  stopRush(eventId: number): Observable<void> {
    return this.http.delete<void>(this.url(eventId, 'rush'));
  }

  reset(eventId: number): Observable<void> {
    return this.http.post<void>(this.url(eventId, 'reset'), {});
  }
}

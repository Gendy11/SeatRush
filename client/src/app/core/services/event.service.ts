import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Movie, ShowingEvent } from '../../shared/models/event';

@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getEvents(): Observable<ShowingEvent[]> {
    return this.http.get<ShowingEvent[]>(this.baseUrl + 'events');
  }

  /** Groups showings by movie name. The API already returns them soonest first. */
  getMovies(): Observable<Movie[]> {
    return this.getEvents().pipe(
      map((events) => {
        const byName = new Map<string, ShowingEvent[]>();
        for (const e of events) {
          byName.set(e.name, [...(byName.get(e.name) ?? []), e]);
        }
        return [...byName].map(([name, showings]) => ({ name, showings }));
      }),
    );
  }
}

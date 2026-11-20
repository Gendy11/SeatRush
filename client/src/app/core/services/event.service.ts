import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Movie } from '../../shared/models/event';
import { EventDetails, Seat } from '../../shared/models/seat';

@Injectable({ providedIn: 'root' })
export class EventService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /** Movies with upcoming showings, already grouped and sorted by the API. */
  getMovies(): Observable<Movie[]> {
    return this.http.get<Movie[]>(this.baseUrl + 'movies');
  }

  getEvent(eventId: number): Observable<EventDetails> {
    return this.http.get<EventDetails>(`${this.baseUrl}events/${eventId}`);
  }

  getSeatMap(eventId: number): Observable<Seat[]> {
    return this.http.get<Seat[]>(`${this.baseUrl}events/${eventId}/seats`);
  }
}

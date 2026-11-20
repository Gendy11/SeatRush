import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Movie } from '../../shared/models/event';
import { BookingSession, EventDetails, SeatMap } from '../../shared/models/seat';

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

  getSeatMap(eventId: number): Observable<SeatMap> {
    return this.http.get<SeatMap>(`${this.baseUrl}events/${eventId}/seats`);
  }

  /** Starts the 7-minute booking session, or resumes the active one (same deadline). */
  startSession(eventId: number): Observable<BookingSession> {
    return this.http.post<BookingSession>(`${this.baseUrl}events/${eventId}/session`, {});
  }

  holdSeat(seatId: number): Observable<{ seatId: number; expiresAt: string }> {
    return this.http.post<{ seatId: number; expiresAt: string }>(`${this.baseUrl}seats/${seatId}/hold`, {});
  }

  releaseSeat(seatId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}seats/${seatId}/hold`);
  }
}

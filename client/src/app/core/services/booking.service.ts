import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Booking, PaymentMethod } from '../../shared/models/booking';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  /**
   * Books the held seats. The same idempotencyKey must be reused for retries of this checkout,
   * so a double click or network retry returns the same booking instead of creating two.
   */
  createBooking(
    eventId: number,
    seatIds: number[],
    paymentMethod: PaymentMethod,
    idempotencyKey: string,
  ): Observable<Booking> {
    return this.http.post<Booking>(
      this.baseUrl + 'bookings',
      { eventId, seatIds, paymentMethod },
      { headers: { 'Idempotency-Key': idempotencyKey } },
    );
  }

  getMyBookings(): Observable<Booking[]> {
    return this.http.get<Booking[]>(this.baseUrl + 'bookings');
  }
}

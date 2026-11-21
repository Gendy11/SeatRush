import { DatePipe } from '@angular/common';
import { Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BookingService } from '../../core/services/booking.service';
import { Booking } from '../../shared/models/booking';
import { PricePipe } from '../../shared/pipes/price.pipe';

@Component({
  selector: 'app-bookings',
  imports: [DatePipe, PricePipe, RouterLink],
  templateUrl: './bookings.component.html',
  styleUrl: './bookings.component.scss',
})
export class BookingsComponent implements OnInit {
  private readonly bookingService = inject(BookingService);

  /** ?new=SR-XXXX after checkout: highlights the booking just made. */
  readonly new = input<string | undefined>();

  protected readonly bookings = signal<Booking[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  ngOnInit(): void {
    this.bookingService.getMyBookings().subscribe({
      next: (bookings) => {
        this.bookings.set(bookings);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }
}

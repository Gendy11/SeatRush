import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { BookingService } from '../../core/services/booking.service';
import { EventService } from '../../core/services/event.service';
import { PaymentMethod } from '../../shared/models/booking';
import { EventDetails, Seat } from '../../shared/models/seat';
import { PricePipe } from '../../shared/pipes/price.pipe';

@Component({
  selector: 'app-checkout',
  imports: [DatePipe, PricePipe, RouterLink],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.scss',
})
export class CheckoutComponent implements OnInit {
  private readonly eventService = inject(EventService);
  private readonly bookingService = inject(BookingService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly id = input.required<string>();

  protected readonly event = signal<EventDetails | null>(null);
  protected readonly seats = signal<Seat[]>([]);
  protected readonly loading = signal(true);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly paymentMethod: PaymentMethod = 'PayAtCounter';

  private readonly idempotencyKey = crypto.randomUUID();

  private clockOffsetMs = 0;
  private readonly deadlineMs = signal<number | null>(null);
  private readonly nowMs = signal(Date.now());

  protected readonly remainingMs = computed(() => {
    const deadline = this.deadlineMs();
    return deadline === null ? 0 : Math.max(0, deadline - (this.nowMs() + this.clockOffsetMs));
  });
  protected readonly expired = computed(() => this.deadlineMs() !== null && this.remainingMs() === 0);
  protected readonly countdown = computed(() => {
    const total = Math.ceil(this.remainingMs() / 1000);
    return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
  });

  protected readonly total = computed(() => this.seats().length * (this.event()?.ticketPrice ?? 0));

  ngOnInit(): void {
    const timer = setInterval(() => this.nowMs.set(Date.now()), 250);
    this.destroyRef.onDestroy(() => clearInterval(timer));

    const eventId = Number(this.id());

    forkJoin({
      event: this.eventService.getEvent(eventId),
      session: this.eventService.startSession(eventId),
      map: this.eventService.getSeatMap(eventId),
    }).subscribe({
      next: ({ event, session, map }) => {
        this.event.set(event);
        this.clockOffsetMs = Date.parse(session.serverNow) - Date.now();
        this.deadlineMs.set(Date.parse(session.expiresAt));
        this.seats.set(map.seats.filter((s) => s.isMine));
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 403) {
          this.router.navigate(['/events', eventId]);
          return;
        }
        this.error.set("Couldn't load your checkout.");
        this.loading.set(false);
      },
    });
  }

  protected confirm(): void {
    const ev = this.event();
    if (!ev || this.submitting() || this.expired() || !this.seats().length) return;

    this.submitting.set(true);
    this.error.set(null);

    this.bookingService
      .createBooking(ev.id, this.seats().map((s) => s.id), this.paymentMethod, this.idempotencyKey)
      .subscribe({
        next: (booking) => this.router.navigate(['/bookings'], { queryParams: { new: booking.reference } }),
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.error.set(
            err.status === 409
              ? (err.error?.title ?? 'Some seats are no longer available.')
              : err.status === 429
                ? 'Too many attempts. Wait a moment and try again.'
                : 'Something went wrong. Please try again.',
          );
        },
      });
  }
}

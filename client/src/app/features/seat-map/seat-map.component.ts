import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { EventService } from '../../core/services/event.service';
import { EventDetails, Seat } from '../../shared/models/seat';

interface SeatRow {
  label: string;
  seats: Seat[];
}

@Component({
  selector: 'app-seat-map',
  imports: [DatePipe, RouterLink],
  templateUrl: './seat-map.component.html',
  styleUrl: './seat-map.component.scss',
})
export class SeatMapComponent implements OnInit {
  private readonly eventService = inject(EventService);

  /** Route param :id (bound via withComponentInputBinding). */
  readonly id = input.required<string>();

  protected readonly event = signal<EventDetails | null>(null);
  protected readonly seats = signal<Seat[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  /** Seats arrive ordered row by row; group them for rendering. */
  protected readonly rows = computed<SeatRow[]>(() => {
    const rows: SeatRow[] = [];
    for (const seat of this.seats()) {
      const last = rows.at(-1);
      if (last?.label === seat.row) last.seats.push(seat);
      else rows.push({ label: seat.row, seats: [seat] });
    }
    return rows;
  });

  /** Widest row; the CSS sizes seats so this many fit across the hall. */
  protected readonly maxCols = computed(() => Math.max(1, ...this.rows().map((r) => r.seats.length)));

  protected readonly available = computed(
    () => this.seats().filter((s) => s.status === 'Available').length,
  );

  ngOnInit(): void {
    const eventId = Number(this.id());
    forkJoin({
      event: this.eventService.getEvent(eventId),
      seats: this.eventService.getSeatMap(eventId),
    }).subscribe({
      next: ({ event, seats }) => {
        this.event.set(event);
        this.seats.set(seats);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }
}

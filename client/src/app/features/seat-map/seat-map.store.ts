import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, fromEvent } from 'rxjs';
import { EventService } from '../../core/services/event.service';
import { LiveEvent, SignalrService } from '../../core/services/signalr.service';
import { BookingSession, EventDetails, Seat, SeatChange, SeatMap } from '../../shared/models/seat';

export interface SeatRow {
  label: string;
  seats: Seat[];
}

/**
 * Everything about one showing's seats for the current page: the snapshot, live SignalR changes (ordered by
 * seq), holds expiring in the browser, the session countdown, and hold/release clicks.
 *
 * Provided by the seat-map page, so both views (2D grid and 3D hall) read the same state and can never disagree.
 */
@Injectable()
export class SeatMapStore {
  private readonly eventService = inject(EventService);
  private readonly signalr = inject(SignalrService);
  private readonly destroyRef = inject(DestroyRef);

  private eventId = 0;

  readonly event = signal<EventDetails | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly message = signal<string | null>(null);

  /** Seats as last received from the server (snapshot + live changes). */
  private readonly seats = signal<Seat[]>([]);

  /** Seats with a hold/release request in flight (ignore extra clicks and echoes meanwhile). */
  private readonly pending = signal<ReadonlySet<number>>(new Set());
  readonly pendingIds = this.pending.asReadonly();

  // ---------- Clock ----------
  /** server time − browser time; applied to every deadline so a wrong PC clock doesn't matter. */
  private clockOffsetMs = 0;
  private readonly nowMs = signal(Date.now());
  private serverNow(): number {
    return this.nowMs() + this.clockOffsetMs;
  }

  // ---------- Countdown ----------
  private readonly sessionExpiresMs = signal<number | null>(null);

  readonly remainingMs = computed(() => {
    const deadline = this.sessionExpiresMs();
    return deadline === null ? 0 : Math.max(0, deadline - this.serverNow());
  });
  readonly expired = computed(() => this.sessionExpiresMs() !== null && this.remainingMs() === 0);
  readonly countdown = computed(() => {
    const total = Math.ceil(this.remainingMs() / 1000);
    return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
  });
  readonly urgent = computed(() => this.remainingMs() > 0 && this.remainingMs() < 60_000);

  // ---------- Live updates ----------
  /** Seq of the current snapshot; per-seat seq of the latest change applied since. */
  private snapshotSeq = 0;
  private readonly seatSeq = new Map<number, number>();
  /** While a snapshot is loading, live changes are queued and applied on top of it afterwards. */
  private buffer: SeatChange[] | null = [];

  // ---------- What the views show ----------
  /** Server state with expired holds turned back into free seats (no server push needed for expiry). */
  readonly view = computed<Seat[]>(() => {
    const now = this.serverNow();
    return this.seats().map((s) =>
      s.status === 'Held' && s.holdExpiresAt && Date.parse(s.holdExpiresAt) <= now
        ? { ...s, status: 'Available', holdExpiresAt: null, isMine: false }
        : s,
    );
  });

  /** Seats arrive ordered row by row; group them for rendering. */
  readonly rows = computed<SeatRow[]>(() => {
    const rows: SeatRow[] = [];
    for (const seat of this.view()) {
      const last = rows.at(-1);
      if (last?.label === seat.row) last.seats.push(seat);
      else rows.push({ label: seat.row, seats: [seat] });
    }
    return rows;
  });

  /** Widest row. */
  readonly maxCols = computed(() => Math.max(1, ...this.rows().map((r) => r.seats.length)));
  readonly available = computed(() => this.view().filter((s) => s.status === 'Available').length);
  readonly mySeats = computed(() => this.view().filter((s) => s.isMine));

  /** Starts the page: live updates first, then event + session + snapshot. Call once. */
  load(eventId: number): void {
    this.eventId = eventId;

    const timer = setInterval(() => this.nowMs.set(Date.now()), 250);
    this.destroyRef.onDestroy(() => clearInterval(timer));

    // 1. Subscribe to live changes FIRST (they queue in `buffer` until the snapshot is in).
    this.signalr
      .watchEvent(eventId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((e: LiveEvent) => (e.type === 'resync' ? this.reloadSeats() : this.onChange(e.change)));

    // 2. Coming back to the tab: timers may have been paused and pushes missed, so re-sync.
    fromEvent(document, 'visibilitychange')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => document.visibilityState === 'visible' && this.reloadSeats());

    // 3. Then load the page.
    forkJoin({
      event: this.eventService.getEvent(eventId),
      session: this.eventService.startSession(eventId),
      map: this.eventService.getSeatMap(eventId),
    }).subscribe({
      next: ({ event, session, map }) => {
        this.event.set(event);
        this.applySession(session);
        this.applySnapshot(map);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  /** Click: free seat → hold it; my seat → release it; anyone else's → nothing. */
  toggle(seat: Seat): void {
    if (this.expired() || this.pending().has(seat.id)) return;

    if (seat.isMine) this.release(seat);
    else if (seat.status === 'Available') this.hold(seat);
  }

  /** After time ran out: start a fresh 7-minute session and reload the map. */
  restart(): void {
    this.message.set(null);
    this.eventService.startSession(this.eventId).subscribe((session) => {
      this.applySession(session);
      this.reloadSeats();
    });
  }

  isPending(seat: Seat): boolean {
    return this.pending().has(seat.id);
  }

  // ---------- Live changes ----------
  private onChange(change: SeatChange): void {
    if (this.buffer) {
      this.buffer.push(change);
      return;
    }

    // Ignore anything not newer than what this seat already reflects (out-of-order or already in the snapshot).
    const known = this.seatSeq.get(change.seatId) ?? this.snapshotSeq;
    if (change.seq <= known) return;
    this.seatSeq.set(change.seatId, change.seq);

    // My own click is in flight: its HTTP response will set the final state.
    if (this.pending().has(change.seatId)) return;

    this.seats.update((seats) =>
      seats.map((s) => {
        if (s.id !== change.seatId) return s;
        // An echo of my own hold keeps it mine (same deadline = same session); anyone else's hold isn't.
        const myEcho =
          change.status === 'Held' &&
          s.isMine &&
          !!s.holdExpiresAt &&
          !!change.expiresAt &&
          Date.parse(s.holdExpiresAt) === Date.parse(change.expiresAt);
        return { ...s, status: change.status, holdExpiresAt: change.expiresAt, isMine: myEcho };
      }),
    );
  }

  private reloadSeats(): void {
    this.buffer ??= [];
    this.eventService.getSeatMap(this.eventId).subscribe({
      next: (map) => this.applySnapshot(map),
      error: () => this.flushBuffer(),
    });
  }

  private applySnapshot(map: SeatMap): void {
    this.clockOffsetMs = Date.parse(map.serverNow) - Date.now();
    this.snapshotSeq = map.seq;
    this.seatSeq.clear();
    this.seats.set(map.seats);
    this.flushBuffer();
  }

  /** Apply changes that arrived while the snapshot was loading; older ones are skipped by seq. */
  private flushBuffer(): void {
    const queued = this.buffer ?? [];
    this.buffer = null;
    for (const change of queued) this.onChange(change);
  }

  // ---------- Clicks ----------
  private hold(seat: Seat): void {
    this.setPending(seat.id, true);
    this.eventService
      .holdSeat(seat.id)
      .subscribe({
        next: (res) => this.updateSeat(seat.id, { status: 'Held', isMine: true, holdExpiresAt: res.expiresAt }),
        error: (err: HttpErrorResponse) => {
          if (err.status === 409 && String(err.error?.title ?? '').includes('session')) {
            this.sessionExpiresMs.set(this.serverNow()); // server says our time is up
          } else if (err.status === 409) {
            this.flash(`Seat ${seat.row}${seat.number} was just taken by someone else.`);
            this.reloadSeats();
          } else {
            this.flash('Something went wrong. Please try again.');
          }
        },
      })
      .add(() => this.setPending(seat.id, false));
  }

  private release(seat: Seat): void {
    this.setPending(seat.id, true);
    this.eventService
      .releaseSeat(seat.id)
      .subscribe({
        // 404 means the hold was already gone (expired): either way the seat is free now.
        next: () => this.updateSeat(seat.id, { status: 'Available', isMine: false, holdExpiresAt: null }),
        error: () => this.updateSeat(seat.id, { status: 'Available', isMine: false, holdExpiresAt: null }),
      })
      .add(() => this.setPending(seat.id, false));
  }

  private applySession(session: BookingSession): void {
    this.clockOffsetMs = Date.parse(session.serverNow) - Date.now();
    this.sessionExpiresMs.set(Date.parse(session.expiresAt));
    this.nowMs.set(Date.now());
  }

  private updateSeat(id: number, patch: Partial<Seat>): void {
    this.seats.update((seats) => seats.map((s) => (s.id === id ? { ...s, ...patch } : s)));
  }

  private setPending(id: number, on: boolean): void {
    this.pending.update((set) => {
      const next = new Set(set);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  private flash(text: string): void {
    this.message.set(text);
    setTimeout(() => this.message() === text && this.message.set(null), 4000);
  }
}

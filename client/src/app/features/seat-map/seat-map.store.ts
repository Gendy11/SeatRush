import { HttpErrorResponse } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, expand, forkJoin, fromEvent, Observable, of, retry, Subscription, switchMap, timer } from 'rxjs';
import { EventService } from '../../core/services/event.service';
import { LiveEvent, SignalrService } from '../../core/services/signalr.service';
import { BookingSession, EventDetails, QueueStatus, Seat, SeatChange, SeatMap } from '../../shared/models/seat';

const QUEUE_POLL_MS = 3000;

export interface SeatRow {
  label: string;
  seats: Seat[];
}

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

  readonly queuePosition = signal<number | null>(null);
  readonly inQueue = computed(() => this.queuePosition() !== null);
  private queueSub?: Subscription;

  private live = false;

  private readonly seats = signal<Seat[]>([]);

  private readonly pending = signal<ReadonlySet<number>>(new Set());
  readonly pendingIds = this.pending.asReadonly();

  private clockOffsetMs = 0;
  private readonly nowMs = signal(Date.now());
  private serverNow(): number {
    return this.nowMs() + this.clockOffsetMs;
  }

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

  private snapshotSeq = 0;
  private readonly seatSeq = new Map<number, number>();

  private buffer: SeatChange[] | null = [];

  readonly view = computed<Seat[]>(() => {
    const now = this.serverNow();
    return this.seats().map((s) =>
      s.status === 'Held' && s.holdExpiresAt && Date.parse(s.holdExpiresAt) <= now
        ? { ...s, status: 'Available', holdExpiresAt: null, isMine: false }
        : s,
    );
  });

  readonly rows = computed<SeatRow[]>(() => {
    const rows: SeatRow[] = [];
    for (const seat of this.view()) {
      const last = rows.at(-1);
      if (last?.label === seat.row) last.seats.push(seat);
      else rows.push({ label: seat.row, seats: [seat] });
    }
    return rows;
  });

  readonly maxCols = computed(() => Math.max(1, ...this.rows().map((r) => r.seats.length)));
  readonly available = computed(() => this.view().filter((s) => s.status === 'Available').length);
  readonly mySeats = computed(() => this.view().filter((s) => s.isMine));

  load(eventId: number): void {
    this.eventId = eventId;

    const clock = setInterval(() => this.nowMs.set(Date.now()), 250);
    this.destroyRef.onDestroy(() => clearInterval(clock));

    this.eventService.getEvent(eventId).subscribe({
      next: (event) => {
        this.event.set(event);
        if (event.hasWaitingRoom) this.waitInLine();
        else this.enter();
      },
      error: () => this.fail(),
    });
  }

  private waitInLine(): void {
    this.queueSub?.unsubscribe();

    const poll = (): Observable<QueueStatus> =>
      timer(QUEUE_POLL_MS + Math.random() * 1000).pipe(
        switchMap(() => this.eventService.getQueueStatus(this.eventId)),
        switchMap((s) => (s.state === 'NotQueued' ? this.eventService.joinQueue(this.eventId) : of(s))),
        retry({ count: 5, delay: QUEUE_POLL_MS }),
      );

    this.queueSub = this.eventService
      .joinQueue(this.eventId)
      .pipe(
        expand((s) => (s.state === 'Admitted' ? EMPTY : poll())),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (s) => {
          if (s.state === 'Admitted') {
            this.queuePosition.set(null);
            this.alertIfHidden();
            this.enter();
          } else {
            this.queuePosition.set(s.position ?? 1);
          }
        },
        error: () => this.fail(),
      });
  }

  private enter(): void {
    this.loading.set(true);
    this.buffer ??= [];

    if (!this.live) {
      this.live = true;

      this.signalr
        .watchEvent(this.eventId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe((e: LiveEvent) => {
          if (e.type === 'change') this.onChange(e.change);
          else if (e.type === 'reset') this.onShowingReset();
          else this.reloadSeats();
        });

      fromEvent(document, 'visibilitychange')
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(() => document.visibilityState === 'visible' && this.reloadSeats());
    }

    forkJoin({
      session: this.eventService.startSession(this.eventId),
      map: this.eventService.getSeatMap(this.eventId),
    }).subscribe({
      next: ({ session, map }) => {
        this.applySession(session);
        this.applySnapshot(map);
        this.loading.set(false);
      },

      error: (err: HttpErrorResponse) => (err.status === 403 ? this.waitInLine() : this.fail()),
    });
  }

  private alertIfHidden(): void {
    if (!document.hidden) return;

    const original = document.title;
    const alert = "🎟 It's your turn!";
    const flasher = setInterval(() => (document.title = document.title === alert ? original : alert), 1000);
    document.title = alert;

    const stop = () => {
      if (document.hidden) return;
      clearInterval(flasher);
      document.title = original;
      document.removeEventListener('visibilitychange', stop);
    };
    document.addEventListener('visibilitychange', stop);
    this.destroyRef.onDestroy(() => {
      clearInterval(flasher);
      document.title = original;
      document.removeEventListener('visibilitychange', stop);
    });
  }

  private fail(): void {
    this.error.set(true);
    this.loading.set(false);
  }

  toggle(seat: Seat): void {
    if (this.expired() || this.pending().has(seat.id)) return;

    if (seat.isMine) this.release(seat);
    else if (seat.status === 'Available') this.hold(seat);
  }

  restart(): void {
    this.message.set(null);

    if (this.event()?.hasWaitingRoom) {
      this.sessionExpiresMs.set(null);
      this.waitInLine();
      return;
    }

    this.eventService.startSession(this.eventId).subscribe((session) => {
      this.applySession(session);
      this.reloadSeats();
    });
  }

  queueAgain(): void {
    this.sessionExpiresMs.set(null);
    this.waitInLine();
  }

  onShowingReset(): void {
    const now = Date.now();
    if (now - this.lastResetMs < 2000) return;
    this.lastResetMs = now;

    this.flash('The showing was reset. Every seat is free again.');
    this.sessionExpiresMs.set(null);
    if (this.event()?.hasWaitingRoom) this.waitInLine();
    else this.enter();
  }

  private lastResetMs = 0;

  isPending(seat: Seat): boolean {
    return this.pending().has(seat.id);
  }

  private onChange(change: SeatChange): void {
    if (this.buffer) {
      this.buffer.push(change);
      return;
    }

    const known = this.seatSeq.get(change.seatId) ?? this.snapshotSeq;
    if (change.seq <= known) return;
    this.seatSeq.set(change.seatId, change.seq);

    if (this.pending().has(change.seatId)) return;

    this.seats.update((seats) =>
      seats.map((s) => {
        if (s.id !== change.seatId) return s;

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

  private flushBuffer(): void {
    const queued = this.buffer ?? [];
    this.buffer = null;
    for (const change of queued) this.onChange(change);
  }

  private hold(seat: Seat): void {
    this.setPending(seat.id, true);
    this.eventService
      .holdSeat(seat.id)
      .subscribe({
        next: (res) => this.updateSeat(seat.id, { status: 'Held', isMine: true, holdExpiresAt: res.expiresAt }),
        error: (err: HttpErrorResponse) => {
          if (err.status === 409 && String(err.error?.title ?? '').includes('session')) {
            this.sessionExpiresMs.set(this.serverNow());
          } else if (err.status === 409) {
            this.flash(`Seat ${seat.row}${seat.number} was just taken by someone else.`);
            this.reloadSeats();
          } else if (err.status === 429) {
            this.flash("You're clicking too fast. Wait a moment and try again.");
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
        next: () => this.updateSeat(seat.id, { status: 'Available', isMine: false, holdExpiresAt: null }),

        error: (err: HttpErrorResponse) => {
          if (err.status === 429) this.flash("You're clicking too fast. Wait a moment and try again.");
          else this.updateSeat(seat.id, { status: 'Available', isMine: false, holdExpiresAt: null });
        },
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

  flash(text: string): void {
    this.message.set(text);
    setTimeout(() => this.message() === text && this.message.set(null), 4000);
  }
}

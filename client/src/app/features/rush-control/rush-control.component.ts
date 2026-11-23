import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, switchMap, takeWhile, timer } from 'rxjs';
import { DemoService, RushStatus } from '../../core/services/demo.service';
import { EventDetails } from '../../shared/models/seat';
import { SeatMapStore } from '../seat-map/seat-map.store';

const STATUS_POLL_MS = 2000;

@Component({
  selector: 'app-rush-control',
  templateUrl: './rush-control.component.html',
  styleUrl: './rush-control.component.scss',
})
export class RushControlComponent implements OnInit {
  private readonly demo = inject(DemoService);
  private readonly store = inject(SeatMapStore);
  private readonly destroyRef = inject(DestroyRef);

  readonly event = input.required<EventDetails>();

  protected readonly status = signal<RushStatus | null>(null);
  protected readonly busy = signal(false);
  protected readonly running = computed(() => this.status()?.running ?? false);
  protected readonly extreme = computed(() => this.event().hasWaitingRoom);

  private pollSub?: Subscription;

  ngOnInit(): void {
    this.watch();
  }

  protected start(): void {
    this.act(this.demo.startRush(this.event().id), (s) => {
      this.status.set(s);

      if (s?.queuePosition != null) this.store.queueAgain();
    });
  }

  protected stop(): void {
    this.act(this.demo.stopRush(this.event().id));
  }

  protected reset(): void {
    this.act(this.demo.reset(this.event().id), () => this.store.onShowingReset());
  }

  private act<T>(request: Observable<T>, done?: (result: T) => void): void {
    if (this.busy()) return;
    this.busy.set(true);

    request.subscribe({
      next: (result) => done?.(result),
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.store.flash(
          err.status === 409 || err.status === 429
            ? (err.error?.title ?? 'Try again in a moment.')
            : "Couldn't reach the demo controls. Try again.",
        );
      },
      complete: () => {
        this.busy.set(false);
        this.watch();
      },
    });
  }

  private watch(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = timer(0, STATUS_POLL_MS)
      .pipe(
        switchMap(() => this.demo.getRush(this.event().id)),
        takeWhile((s) => s.running, true),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({ next: (s) => this.status.set(s), error: () => this.status.set(null) });
  }
}

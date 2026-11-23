import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Seat } from '../../shared/models/seat';
import { PricePipe } from '../../shared/pipes/price.pipe';
import { SeatMapStore } from '../seat-map/seat-map.store';
import { HallBottomSheetComponent } from './overlay/hall-bottom-sheet.component';
import { HallLegendComponent } from './overlay/hall-legend.component';
import { HallTopBarComponent } from './overlay/hall-top-bar.component';
import { HallScene } from './scene/hall-scene';
import { SeatVisualState } from './scene/seat-colors';
import { buildHallLayout } from './scene/seat-layout';

interface Hovered {
  seatId: number;
  x: number;
  y: number;
}

interface Tooltip {
  seat: Seat;
  pending: boolean;
  x: number;
  y: number;
}

@Component({
  selector: 'app-hall-3d',
  imports: [PricePipe, HallTopBarComponent, HallBottomSheetComponent, HallLegendComponent],
  templateUrl: './hall-3d.component.html',
  styleUrl: './hall-3d.component.scss',
})
export class Hall3dComponent {
  protected readonly store = inject(SeatMapStore);
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');

  private readonly scene = signal<HallScene | null>(null);
  protected readonly ready = signal(false);

  private readonly hovered = signal<Hovered | null>(null);

  protected readonly tooltip = computed<Tooltip | null>(() => {
    const h = this.hovered();
    if (!h) return null;
    const seat = this.store.view().find((s) => s.id === h.seatId);
    return seat ? { seat, pending: this.store.pendingIds().has(seat.id), x: h.x, y: h.y } : null;
  });
  protected readonly seatView = signal(false);
  protected readonly introRunning = signal(false);

  protected readonly posterColor = signal<string | null>(null);
  private readonly lastClickedId = signal<number | null>(null);

  protected readonly focusSeat = computed(() => {
    const mine = this.store.mySeats();
    return mine.find((s) => s.id === this.lastClickedId()) ?? mine.at(-1) ?? null;
  });

  private readonly seatSetKey = computed(() =>
    this.store
      .rows()
      .map((r) => `${r.label}:${r.seats.map((s) => s.id).join(',')}`)
      .join('|'),
  );

  constructor() {
    afterNextRender(() =>
      this.scene.set(
        new HallScene(this.host().nativeElement, {
          hover: (seatId, x, y) => this.onHover(seatId, x, y),
          select: (seatId) => this.onSelect(seatId),
          intro: (running) => this.introRunning.set(running),
          posterColor: (css) => this.posterColor.set(css),
        }),
      ),
    );

    inject(DestroyRef).onDestroy(() => this.scene()?.dispose());

    effect(() => {
      const scene = this.scene();
      const key = this.seatSetKey();
      const event = this.store.event();
      if (!scene || !event || !key) return;

      const rows = untracked(() => this.store.rows());
      const layout = buildHallLayout(rows.map((r) => ({ label: r.label, seats: r.seats })));
      this.ready.set(false);
      void scene.build(layout, event.backdropUrl || event.posterUrl, event.movieTitle).then(() => {
        scene.updateSeats(visualStates(untracked(() => this.store.view())));
        this.ready.set(true);
      });
    });

    effect(() => {
      const seats = this.store.view();
      if (this.ready()) this.scene()?.updateSeats(visualStates(seats));
    });

    effect(() => {
      if (this.seatView() && !this.focusSeat()) void this.backToHall();
    });
  }

  protected skipIntro(): void {
    this.scene()?.skipIntro();
  }

  protected viewFromSeat(): void {
    const seat = this.focusSeat();
    const scene = this.scene();
    if (!seat || !scene) return;
    this.seatView.set(true);
    this.hovered.set(null);
    void scene.viewFromSeat(seat.id);
  }

  protected async backToHall(): Promise<void> {
    await this.scene()?.backToHall();
    this.seatView.set(false);
  }

  private onHover(seatId: number | null, x: number, y: number): void {
    const seat = seatId === null ? undefined : this.store.view().find((s) => s.id === seatId);
    this.hovered.set(seat ? { seatId: seat.id, x, y } : null);
    this.scene()?.setCursor(
      seat && isClickable(seat) && !this.store.expired() ? 'pointer' : 'default',
    );
  }

  private onSelect(seatId: number): void {
    const seat = this.store.view().find((s) => s.id === seatId);
    if (!seat || this.store.expired() || !isClickable(seat)) return;

    if (seat.isMine) {
      if (this.lastClickedId() === seatId) this.lastClickedId.set(null);
    } else {
      this.lastClickedId.set(seatId);
    }
    this.store.toggle(seat);
  }
}

function isClickable(seat: Seat): boolean {
  return seat.isMine || seat.status === 'Available';
}

function visualStates(seats: readonly Seat[]): Map<number, SeatVisualState> {
  const map = new Map<number, SeatVisualState>();
  for (const s of seats) {
    map.set(
      s.id,
      s.isMine
        ? 'mine'
        : s.status === 'Held'
          ? 'held'
          : s.status === 'Booked'
            ? 'booked'
            : 'available',
    );
  }
  return map;
}

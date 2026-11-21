import { DatePipe } from '@angular/common';
import {
  afterNextRender,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  Injector,
  input,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { PricePipe } from '../../shared/pipes/price.pipe';
import { Hall3dComponent } from '../hall-3d/hall-3d.component';
import { supportsWebGL } from '../hall-3d/webgl';
import { SeatMapStore } from './seat-map.store';

type ViewMode = '3d' | '2d';

/** Tablets and laptops get the 3D hall; on phones it's too cramped, so they get the 2D seat map. */
const WIDE_SCREEN = '(min-width: 768px)';

/**
 * Seat-map page: session bar, showing info and the hall. The hall is either the 3D auditorium or the
 * 2D grid; both read the same SeatMapStore (provided here, one per page).
 */
@Component({
  selector: 'app-seat-map',
  imports: [DatePipe, PricePipe, RouterLink, Hall3dComponent],
  providers: [SeatMapStore],
  templateUrl: './seat-map.component.html',
  styleUrl: './seat-map.component.scss',
})
export class SeatMapComponent implements OnInit {
  protected readonly store = inject(SeatMapStore);
  private readonly injector = inject(Injector);

  /** Route param :id (bound via withComponentInputBinding). */
  readonly id = input.required<string>();

  protected readonly webgl = supportsWebGL();

  /** Tracks the screen width live, so resizing across 768px switches like a real device would. */
  private readonly wideQuery = window.matchMedia(WIDE_SCREEN);
  protected readonly isWide = signal(this.wideQuery.matches);

  /** Default: 3D on tablets/laptops, 2D on phones. The toggle lets anyone switch. */
  protected readonly mode = signal<ViewMode>(this.webgl && this.isWide() ? '3d' : '2d');

  /** 3D is only actually rendered on a wide screen with WebGL; otherwise "3D" shows a short explanation. */
  protected readonly show3d = computed(() => this.mode() === '3d' && this.webgl && this.isWide());

  /** The scrollable 2D hall; on narrow screens wide rows scroll sideways inside it. */
  private readonly hall = viewChild<ElementRef<HTMLElement>>('hall');

  constructor() {
    const onWidth = (e: MediaQueryListEvent) => this.isWide.set(e.matches);
    this.wideQuery.addEventListener('change', onWidth);
    inject(DestroyRef).onDestroy(() => this.wideQuery.removeEventListener('change', onWidth));

    // Phones, 2D view: when a hall is wider than the screen (IMAX), open it scrolled to the middle of the rows.
    effect(() => {
      const el = this.hall()?.nativeElement;
      if (!el) return;
      afterNextRender(() => (el.scrollLeft = (el.scrollWidth - el.clientWidth) / 2), { injector: this.injector });
    });
  }

  ngOnInit(): void {
    this.store.load(Number(this.id()));
  }
}

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
import { RushControlComponent } from '../rush-control/rush-control.component';
import { WaitingRoomComponent } from '../waiting-room/waiting-room.component';
import { SeatMapStore } from './seat-map.store';

type ViewMode = '3d' | '2d';

const WIDE_SCREEN = '(min-width: 768px)';

@Component({
  selector: 'app-seat-map',
  imports: [DatePipe, PricePipe, RouterLink, Hall3dComponent, WaitingRoomComponent, RushControlComponent],
  providers: [SeatMapStore],
  templateUrl: './seat-map.component.html',
  styleUrl: './seat-map.component.scss',
})
export class SeatMapComponent implements OnInit {
  protected readonly store = inject(SeatMapStore);
  private readonly injector = inject(Injector);

  readonly id = input.required<string>();

  protected readonly webgl = supportsWebGL();

  private readonly wideQuery = window.matchMedia(WIDE_SCREEN);
  protected readonly isWide = signal(this.wideQuery.matches);

  protected readonly mode = signal<ViewMode>(this.webgl && this.isWide() ? '3d' : '2d');

  protected readonly show3d = computed(() => this.mode() === '3d' && this.webgl && this.isWide());

  private readonly hall = viewChild<ElementRef<HTMLElement>>('hall');

  constructor() {
    const onWidth = (e: MediaQueryListEvent) => this.isWide.set(e.matches);
    this.wideQuery.addEventListener('change', onWidth);
    inject(DestroyRef).onDestroy(() => this.wideQuery.removeEventListener('change', onWidth));

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

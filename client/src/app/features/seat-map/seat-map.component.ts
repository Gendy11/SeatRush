import { DatePipe } from '@angular/common';
import {
  afterNextRender,
  Component,
  computed,
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

  protected readonly mode = signal<ViewMode>(this.webgl ? '3d' : '2d');

  protected readonly show3d = computed(() => this.mode() === '3d' && this.webgl);

  private readonly hall = viewChild<ElementRef<HTMLElement>>('hall');

  constructor() {
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

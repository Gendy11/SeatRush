import { DatePipe } from '@angular/common';
import { Component, computed, effect, input, signal } from '@angular/core';
import { EventDetails } from '../../shared/models/seat';

@Component({
  selector: 'app-waiting-room',
  imports: [DatePipe],
  templateUrl: './waiting-room.component.html',
  styleUrl: './waiting-room.component.scss',
})
export class WaitingRoomComponent {
  readonly event = input.required<EventDetails>();

  readonly position = input.required<number>();

  private readonly startPosition = signal<number | null>(null);

  protected readonly ahead = computed(() => Math.max(0, this.position() - 1));

  protected readonly progress = computed(() => {
    const start = this.startPosition() ?? this.position();
    return start <= 1 ? 1 : Math.min(1, (start - this.position()) / (start - 1));
  });

  constructor() {
    effect(() => {
      const p = this.position();
      this.startPosition.update((s) => (s === null || p > s ? p : s));
    });
  }
}

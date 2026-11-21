import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { SeatMapStore } from '../../seat-map/seat-map.store';

/** Top of the 3D hall: big condensed title, show time and hall, live availability. */
@Component({
  selector: 'app-hall-top-bar',
  imports: [DatePipe],
  template: `
    @if (store.event(); as ev) {
      <div class="top">
        <h1 class="title">{{ ev.movieTitle }}</h1>
        <p class="meta">
          <span class="pill">{{ ev.ageRating }}</span>
          <span>{{ ev.showTime | date: 'EEE d MMM · HH:mm' }}</span>
          <span class="dot"></span>
          <span>{{ ev.venue }}</span>
          <span class="dot"></span>
          <span
            ><strong>{{ store.available() }}</strong> / {{ ev.totalSeats }} available</span
          >
        </p>
      </div>
    }
  `,
  styles: `
    .top {
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      padding: 18px 22px 40px;
      background: linear-gradient(180deg, rgba(7, 7, 10, 0.85) 0%, transparent 100%);
      pointer-events: none;
    }
    .title {
      margin: 0;
      font-family: var(--font-display);
      font-weight: 400;
      font-size: clamp(2rem, 5vw, 3.4rem);
      line-height: 0.95;
      letter-spacing: 0.02em;
      /* Glow in the poster's colour (set by the 3D hall), so each movie's hall has its own mood. */
      text-shadow: 0 0 36px var(--poster, transparent);
    }
    .meta {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px 10px;
      margin: 6px 0 0;
      color: var(--text-muted);
      font-size: 0.8rem;
    }
    strong {
      color: var(--accent);
    }
    .pill {
      padding: 1px 8px;
      border: 1px solid var(--text-muted);
      border-radius: 999px;
      color: var(--text);
      font-weight: 700;
      font-size: 0.7rem;
    }
    .dot {
      width: 3px;
      height: 3px;
      border-radius: 50%;
      background: var(--text-muted);
    }
  `,
})
export class HallTopBarComponent {
  protected readonly store = inject(SeatMapStore);
}

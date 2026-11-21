import { Component } from '@angular/core';

/** Seat-state key. Colours match SEAT_COLORS in the scene. */
@Component({
  selector: 'app-hall-legend',
  template: `
    <ul class="legend" aria-label="Seat colours">
      <li><span class="swatch" style="background: #b9b3a8"></span>Free</li>
      <li><span class="swatch swatch--mine"></span>Yours</li>
      <li><span class="swatch" style="background: #8a6524"></span>Held</li>
      <li><span class="swatch" style="background: #2a2a30"></span>Sold</li>
    </ul>
  `,
  styles: `
    .legend {
      position: absolute;
      top: 18px;
      right: 18px;
      display: flex;
      gap: 12px;
      margin: 0;
      padding: 8px 12px;
      list-style: none;
      border-radius: 999px;
      background: rgba(12, 12, 16, 0.6);
      backdrop-filter: blur(8px);
      font-size: 0.72rem;
      color: var(--text-muted);
      pointer-events: none;
    }
    li {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .swatch {
      width: 10px;
      height: 10px;
      border-radius: 3px;
    }
    .swatch--mine {
      background: var(--accent);
      box-shadow: 0 0 8px var(--accent);
    }
    @media (max-width: 640px) {
      .legend {
        top: 118px;
        left: 16px;
        right: auto;
        gap: 10px;
        padding: 6px 10px;
        font-size: 0.68rem;
      }
    }
  `,
})
export class HallLegendComponent {}

import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PricePipe } from '../../../shared/pipes/price.pipe';
import { SeatMapStore } from '../../seat-map/seat-map.store';

/** Bottom of the 3D hall: countdown, my seats as chips, total and Checkout. */
@Component({
  selector: 'app-hall-bottom-sheet',
  imports: [PricePipe, RouterLink],
  template: `
    @if (store.event(); as ev) {
      @let mine = store.mySeats();
      @let expired = store.expired();
      <div class="sheet" [class.sheet--urgent]="store.urgent()">
        <div class="timer" role="timer">
          <span class="label">{{ expired ? "Time's up" : 'Seats held for' }}</span>
          <span class="value">{{ store.countdown() }}</span>
        </div>

        <div class="seats">
          @for (s of mine; track s.id) {
            <span class="chip">{{ s.row }}{{ s.number }}</span>
          } @empty {
            <span class="hint">Tap a seat to hold it</span>
          }
        </div>

        @if (mine.length) {
          <span class="total">{{ mine.length * ev.ticketPrice | price }}</span>
        }
        <a
          class="btn"
          [class.btn--disabled]="!mine.length || expired"
          [attr.aria-disabled]="!mine.length || expired"
          [routerLink]="mine.length && !expired ? ['/events', ev.id, 'checkout'] : null"
          >Checkout</a
        >
      </div>
    }
  `,
  styles: `
    .sheet {
      position: absolute;
      left: 16px;
      right: 16px;
      bottom: 16px;
      display: flex;
      align-items: center;
      gap: 18px;
      padding: 12px 14px 12px 18px;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      background: rgba(12, 12, 16, 0.78);
      backdrop-filter: blur(12px);
    }
    .timer {
      display: flex;
      flex-direction: column;
      min-width: 88px;
    }
    .label {
      font-size: 0.62rem;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      color: var(--text-muted);
    }
    .value {
      font-family: var(--font-display);
      font-size: 2rem;
      line-height: 1;
      color: var(--accent);
      font-variant-numeric: tabular-nums;
    }
    .sheet--urgent .value {
      color: #f87171;
    }
    .seats {
      flex: 1;
      display: flex;
      gap: 6px;
      overflow-x: auto;
      min-width: 0;
    }
    .chip {
      padding: 3px 9px;
      border-radius: 6px;
      background: rgba(245, 197, 24, 0.15);
      border: 1px solid rgba(245, 197, 24, 0.45);
      font-size: 0.75rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .hint {
      color: var(--text-muted);
      font-size: 0.85rem;
    }
    .total {
      font-weight: 800;
      white-space: nowrap;
    }
    @media (max-width: 640px) {
      .sheet {
        flex-wrap: wrap;
        left: 8px;
        right: 8px;
        bottom: 8px;
        gap: 8px 12px;
        padding: 10px 12px;
      }
      .timer {
        min-width: 0;
      }
      .value {
        font-size: 1.7rem;
      }
      .seats {
        order: 3;
        flex-basis: 100%;
      }
      .seats:empty {
        display: none;
      }
      .total {
        margin-left: auto;
      }
      .btn {
        padding: 10px 16px;
        font-size: 0.78rem;
      }
    }
  `,
})
export class HallBottomSheetComponent {
  protected readonly store = inject(SeatMapStore);
}

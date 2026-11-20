export type SeatStatus = 'Available' | 'Held' | 'Booked';

/** One seat of a showing. */
export interface Seat {
  id: number;
  row: string;
  number: number;
  status: SeatStatus;
  /** For Held seats: when the hold ends (UTC ISO). The browser frees the seat itself at that time. */
  holdExpiresAt: string | null;
  /** Held by the current user. */
  isMine: boolean;
}

/** GET /api/events/{id}/seats */
export interface SeatMap {
  /** Change counter at snapshot time: live changes with a higher seq are applied on top. */
  seq: number;
  serverNow: string;
  seats: Seat[];
}

/** Live "SeatChanged" message from the SignalR hub. */
export interface SeatChange {
  seatId: number;
  status: SeatStatus;
  expiresAt: string | null;
  seq: number;
}

/** A showing with its movie and venue, as returned by GET /api/events/{id}. */
export interface EventDetails {
  id: number;
  movieTitle: string;
  posterUrl: string;
  ageRating: string;
  durationMinutes: number;
  venue: string;
  showTime: string;
  totalSeats: number;
}

/** The user's 7-minute booking window for one showing. Times are UTC ISO strings. */
export interface BookingSession {
  eventId: number;
  expiresAt: string;
  serverNow: string;
}

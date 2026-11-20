export type SeatStatus = 'Available' | 'Held' | 'Booked';

/** One seat of a showing, as returned by GET /api/events/{id}/seats. */
export interface Seat {
  id: number;
  row: string;
  number: number;
  status: SeatStatus;
  version: number;
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

export type SeatStatus = 'Available' | 'Held' | 'Booked';

export interface Seat {
  id: number;
  row: string;
  number: number;
  status: SeatStatus;

  holdExpiresAt: string | null;

  isMine: boolean;
}

export interface SeatMap {
  seq: number;
  serverNow: string;
  seats: Seat[];
}

export interface SeatChange {
  seatId: number;
  status: SeatStatus;
  expiresAt: string | null;
  seq: number;
}

export interface EventDetails {
  id: number;
  movieTitle: string;
  posterUrl: string;

  backdropUrl: string;
  ageRating: string;
  durationMinutes: number;
  venue: string;
  showTime: string;
  totalSeats: number;

  ticketPrice: number;

  hasWaitingRoom: boolean;
}

export interface QueueStatus {
  state: 'NotQueued' | 'Waiting' | 'Admitted';

  position: number | null;

  admittedUntil: string | null;
  serverNow: string;
}

export interface BookingSession {
  eventId: number;
  expiresAt: string;
  serverNow: string;
}

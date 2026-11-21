export type PaymentMethod = 'PayAtCounter';

export interface BookedSeat {
  seatId: number;
  row: string;
  number: number;
  price: number;
}

/** A booking, as returned by POST/GET /api/bookings. */
export interface Booking {
  id: number;
  reference: string;
  eventId: number;
  movieTitle: string;
  posterUrl: string;
  venue: string;
  showTime: string;
  seats: BookedSeat[];
  totalAmount: number;
  paymentMethod: PaymentMethod;
  status: 'Confirmed';
  createdAt: string;
}

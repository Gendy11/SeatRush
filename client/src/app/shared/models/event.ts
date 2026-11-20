/** One showing, as returned by GET /api/events. showTime is a UTC ISO string. */
export interface ShowingEvent {
  id: number;
  name: string;
  venue: string;
  showTime: string;
  totalSeats: number;
}

/** UI grouping: one movie with all its upcoming showings. */
export interface Movie {
  name: string;
  showings: ShowingEvent[];
}

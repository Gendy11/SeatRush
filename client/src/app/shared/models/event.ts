/** One showing of a movie. showTime is a UTC ISO string. */
export interface Showing {
  eventId: number;
  venue: string;
  showTime: string;
}

/** A movie with its upcoming showings, as returned by GET /api/movies. */
export interface Movie {
  id: number;
  title: string;
  posterUrl: string;
  /** Landscape still, used behind the hero. */
  backdropUrl: string;
  ageRating: string;
  durationMinutes: number;
  showings: Showing[];
}

export interface Showing {
  eventId: number;
  venue: string;
  showTime: string;
}

export interface Movie {
  id: number;
  title: string;
  posterUrl: string;

  backdropUrl: string;
  ageRating: string;
  durationMinutes: number;
  showings: Showing[];
}

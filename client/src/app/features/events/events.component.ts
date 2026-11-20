import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { EventService } from '../../core/services/event.service';
import { Movie } from '../../shared/models/event';

@Component({
  selector: 'app-events',
  imports: [DatePipe],
  templateUrl: './events.component.html',
  styleUrl: './events.component.scss',
})
export class EventsComponent implements OnInit {
  private readonly eventService = inject(EventService);

  protected readonly movies = signal<Movie[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  ngOnInit(): void {
    this.eventService.getMovies().subscribe({
      next: (movies) => {
        this.movies.set(movies);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  /** Placeholder poster until events have real artwork: a stable gradient derived from the title. */
  protected posterGradient(name: string): string {
    let hash = 0;
    for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
    const hue = hash % 360;
    return `linear-gradient(160deg, hsl(${hue} 60% 38%) 0%, hsl(${(hue + 40) % 360} 55% 18%) 60%, #0b0b0d 100%)`;
  }
}

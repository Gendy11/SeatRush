import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EventService } from '../../core/services/event.service';
import { Movie } from '../../shared/models/event';

@Component({
  selector: 'app-events',
  imports: [DatePipe, RouterLink],
  templateUrl: './events.component.html',
  styleUrl: './events.component.scss',
})
export class EventsComponent implements OnInit {
  private readonly eventService = inject(EventService);

  protected readonly movies = signal<Movie[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal(false);

  /** Hero: the movie with the soonest showing (the API orders movies that way). */
  protected readonly featured = computed(() => this.movies()[0]);
  protected readonly showingCount = computed(() =>
    this.movies().reduce((n, m) => n + m.showings.length, 0),
  );

  protected scrollToMovie(id: number): void {
    document.getElementById(`movie-${id}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

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

  /** 128 -> "2h 8m" */
  protected formatDuration(minutes: number): string {
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return h ? `${h}h ${m}m` : `${m}m`;
  }
}

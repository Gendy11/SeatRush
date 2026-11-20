import { Routes } from '@angular/router';
import { EventsComponent } from './features/events/events.component';
import { SeatMapComponent } from './features/seat-map/seat-map.component';

export const routes: Routes = [
  { path: '', component: EventsComponent },
  { path: 'events/:id', component: SeatMapComponent },
  { path: '**', redirectTo: '' },
];

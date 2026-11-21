import { Routes } from '@angular/router';
import { BookingsComponent } from './features/bookings/bookings.component';
import { CheckoutComponent } from './features/checkout/checkout.component';
import { EventsComponent } from './features/events/events.component';
import { SeatMapComponent } from './features/seat-map/seat-map.component';

export const routes: Routes = [
  { path: '', component: EventsComponent },
  { path: 'events/:id', component: SeatMapComponent },
  { path: 'events/:id/checkout', component: CheckoutComponent },
  { path: 'bookings', component: BookingsComponent },
  { path: '**', redirectTo: '' },
];

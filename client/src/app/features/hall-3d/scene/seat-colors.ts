import { Color } from 'three';

export type SeatVisualState = 'available' | 'held' | 'booked' | 'mine';

export const SEAT_COLORS: Record<SeatVisualState, Color> = {
  available: new Color('#b9b3a8'),
  held: new Color('#8a6524'),
  booked: new Color('#1d1d22'),
  mine: new Color('#f5c518'),
};

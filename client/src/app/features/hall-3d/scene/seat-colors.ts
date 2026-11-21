import { Color } from 'three';

/** One visual state per seat. "mine" is the app's gold accent, same as the 2D map. */
export type SeatVisualState = 'available' | 'held' | 'booked' | 'mine';

export const SEAT_COLORS: Record<SeatVisualState, Color> = {
  available: new Color('#b9b3a8'), // neutral warm grey
  held: new Color('#8a6524'), // dim amber: someone else is holding it
  booked: new Color('#1d1d22'), // sold: dark, almost disappears
  mine: new Color('#f5c518'), // accent
};

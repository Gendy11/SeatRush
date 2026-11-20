import { Injectable } from '@angular/core';

const STORAGE_KEY = 'seatrush.userId';

/**
 * TEMPORARY anonymous identity: a random id created once per browser and kept in localStorage.
 * Replaced by a proper anonymous token in FR23.
 */
@Injectable({ providedIn: 'root' })
export class UserService {
  readonly userId = this.loadOrCreate();

  private loadOrCreate(): string {
    try {
      const existing = localStorage.getItem(STORAGE_KEY);
      if (existing) return existing;
      const id = crypto.randomUUID();
      localStorage.setItem(STORAGE_KEY, id);
      return id;
    } catch {
      // Storage blocked (private mode etc.): fall back to an id for this tab only.
      return crypto.randomUUID();
    }
  }
}

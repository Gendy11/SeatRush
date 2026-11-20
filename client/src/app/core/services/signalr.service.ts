import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SeatChange } from '../../shared/models/seat';

/** What the seat map receives from the live connection. */
export type LiveEvent =
  | { type: 'change'; change: SeatChange }
  /** Connection came back after a drop: changes may have been missed, reload the map. */
  | { type: 'resync' };

@Injectable({ providedIn: 'root' })
export class SignalrService {
  /**
   * Opens a live connection for one showing. Joining the group happens *before* the caller
   * loads the seat map, so no change can slip between the snapshot and the first push.
   * Unsubscribing leaves the group and closes the connection.
   */
  watchEvent(eventId: number): Observable<LiveEvent> {
    return new Observable<LiveEvent>((subscriber) => {
      const connection: HubConnection = new HubConnectionBuilder()
        .withUrl(environment.hubUrl)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on('SeatChanged', (change: SeatChange) => subscriber.next({ type: 'change', change }));

      // Group membership is lost on reconnect: re-join, then tell the page to reload the map.
      connection.onreconnected(async () => {
        await connection.invoke('JoinEvent', eventId);
        subscriber.next({ type: 'resync' });
      });

      connection
        .start()
        .then(() => connection.invoke('JoinEvent', eventId))
        .catch((err) => console.warn('Live seat updates unavailable', err));

      return () => {
        if (connection.state === HubConnectionState.Connected) {
          connection.invoke('LeaveEvent', eventId).finally(() => connection.stop());
        } else {
          connection.stop();
        }
      };
    });
  }
}

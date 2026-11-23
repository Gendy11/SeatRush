import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SeatChange } from '../../shared/models/seat';

export type LiveEvent =
  | { type: 'change'; change: SeatChange }

  | { type: 'resync' }

  | { type: 'reset' };

@Injectable({ providedIn: 'root' })
export class SignalrService {
  watchEvent(eventId: number): Observable<LiveEvent> {
    return new Observable<LiveEvent>((subscriber) => {
      const connection: HubConnection = new HubConnectionBuilder()
        .withUrl(environment.hubUrl)
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on('SeatChanged', (change: SeatChange) => subscriber.next({ type: 'change', change }));
      connection.on('ShowingReset', () => subscriber.next({ type: 'reset' }));

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

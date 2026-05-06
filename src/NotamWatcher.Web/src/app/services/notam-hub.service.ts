import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { Notam, SEVERITY_LABELS } from '../models/notam.model';
import { environment } from '../../environments/environment';

export type HubStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting' | 'error';

@Injectable({ providedIn: 'root' })
export class NotamHubService implements OnDestroy {
  private connection: signalR.HubConnection | null = null;
  private currentRouteKey: string | null = null;

  readonly status$ = new BehaviorSubject<HubStatus>('disconnected');
  readonly notamNew$ = new Subject<Notam>();
  readonly notamUpdated$ = new Subject<Notam>();

  private buildConnection(): signalR.HubConnection {
    return new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}/hubs/notams`, {
        headers: { 'X-Hub-Token': 'dev-token' },
        // Fall back to SSE → long-polling if WebSocket is unavailable (e.g. some proxies).
        transport:
          signalR.HttpTransportType.WebSockets |
          signalR.HttpTransportType.ServerSentEvents |
          signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect({
        // Custom backoff: 0s, 2s, 10s, 30s, then 30s indefinitely.
        nextRetryDelayInMilliseconds: (ctx) => {
          const delays = [0, 2000, 10000, 30000];
          return delays[ctx.previousRetryCount] ?? 30000;
        },
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }

  async connect(routeKey: string): Promise<void> {
    if (this.connection) {
      await this.disconnect();
    }

    this.currentRouteKey = routeKey;
    this.status$.next('connecting');
    this.connection = this.buildConnection();

    this.connection.on('NotamNew', (raw: Notam) =>
      this.notamNew$.next(this.enrich(raw))
    );
    this.connection.on('NotamUpdated', (raw: Notam) =>
      this.notamUpdated$.next(this.enrich(raw))
    );

    this.connection.onreconnecting(() => this.status$.next('reconnecting'));
    this.connection.onreconnected(async () => {
      this.status$.next('connected');
      if (this.currentRouteKey) {
        await this.connection!.invoke('SubscribeToRoute', this.currentRouteKey);
      }
    });
    this.connection.onclose(() => this.status$.next('disconnected'));

    try {
      await this.connection.start();
      await this.connection.invoke('SubscribeToRoute', routeKey);
      this.status$.next('connected');
    } catch {
      this.status$.next('error');
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      this.connection.off('NotamNew');
      this.connection.off('NotamUpdated');
      await this.connection.stop();
      this.connection = null;
    }
    this.currentRouteKey = null;
    this.status$.next('disconnected');
  }

  ngOnDestroy(): void {
    this.disconnect();
  }

  private enrich(notam: Notam): Notam {
    return { ...notam, severityLabel: SEVERITY_LABELS[notam.severity] ?? 'Unknown' };
  }
}

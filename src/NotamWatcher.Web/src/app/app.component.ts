import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';

import { Notam } from './models/notam.model';
import { NotamHubService, HubStatus } from './services/notam-hub.service';
import { NotamApiService } from './services/notam-api.service';
import { RouteApiService } from './services/route-api.service';
import { ConnectionStatusComponent } from './components/connection-status/connection-status.component';
import { NotamCardComponent } from './components/notam-card/notam-card.component';
import { RouteInputComponent } from './components/route-input/route-input.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    ConnectionStatusComponent,
    NotamCardComponent,
    RouteInputComponent,
  ],
  templateUrl: './app.component.html',
})
export class AppComponent implements OnInit, OnDestroy {
  notams: Notam[] = [];
  hubStatus: HubStatus = 'disconnected';
  activeRoute: string[] = [];
  isLoading = false;
  newCount = 0;   // badge counter for items added since last view

  private subs = new Subscription();

  constructor(
    private hub: NotamHubService,
    private notamApi: NotamApiService,
    private routeApi: RouteApiService,
  ) {}

  ngOnInit(): void {
    this.subs.add(
      this.hub.status$.subscribe(s => (this.hubStatus = s))
    );
    this.subs.add(
      this.hub.notamNew$.subscribe(n => {
        this.notams = [n, ...this.notams];
        this.newCount++;
      })
    );
    this.subs.add(
      this.hub.notamUpdated$.subscribe(updated => {
        this.notams = this.notams.map(n =>
          n.notamNumber === updated.notamNumber ? updated : n
        );
      })
    );
  }

  async onRouteSelected(codes: string[]): Promise<void> {
    this.isLoading = true;
    this.newCount = 0;
    this.activeRoute = codes;

    // Sort codes to produce a stable route key matching server-side normalization.
    const routeKey = [...codes].sort().join('-');

    // Register the route in the API (upsert — safe to call repeatedly).
    this.routeApi.createRoute(codes).subscribe({
      error: err => console.error('Could not register route', err),
    });

    // Load existing NOTAMs for this route from REST, then switch to live updates.
    this.notamApi.getNotams(codes).subscribe({
      next: notams => {
        this.notams = notams;
        this.isLoading = false;
      },
      error: () => {
        this.notams = [];
        this.isLoading = false;
      },
    });

    await this.hub.connect(routeKey);
  }

  async onDisconnect(): Promise<void> {
    await this.hub.disconnect();
    this.notams = [];
    this.activeRoute = [];
    this.newCount = 0;
  }

  trackByNumber(_: number, n: Notam): string {
    return n.notamNumber;
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.hub.disconnect();
  }
}

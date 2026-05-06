import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface WatchedRoute {
  id: number;
  routeKey: string;
  icaoCodes: string[];
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class RouteApiService {
  private readonly base = `${environment.apiUrl}/api/routes`;

  constructor(private http: HttpClient) {}

  getRoutes(): Observable<WatchedRoute[]> {
    return this.http.get<WatchedRoute[]>(this.base);
  }

  createRoute(icaoCodes: string[]): Observable<WatchedRoute> {
    return this.http.post<WatchedRoute>(this.base, { icaoCodes });
  }

  deleteRoute(routeKey: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${routeKey}`);
  }
}

import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { Notam, SEVERITY_LABELS } from '../models/notam.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class NotamApiService {
  private readonly base = `${environment.apiUrl}/api/notams`;

  constructor(private http: HttpClient) {}

  getNotams(icaoCodes: string[]): Observable<Notam[]> {
    const locations = icaoCodes.join(',');
    return this.http
      .get<Notam[]>(`${this.base}?locations=${locations}`)
      .pipe(map(items => items.map(n => ({ ...n, severityLabel: SEVERITY_LABELS[n.severity] ?? 'Unknown' }))));
  }
}

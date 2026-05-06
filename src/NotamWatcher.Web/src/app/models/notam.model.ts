export type NotamSeverity = 'Unknown' | 'Advisory' | 'Caution' | 'Warning' | 'Critical';
export type NotamClassification = 'Unknown' | 'Aerodrome' | 'Enroute' | 'Warning' | 'Checklist' | 'Trigger';

export interface Notam {
  id: number;
  notamNumber: string;
  icaoLocation: string;
  qCode: string;
  subject: string;
  condition: string;
  startValidity: string | null;
  endValidity: string | null;
  freeText: string;
  rawText: string;
  severity: number;           // enum ordinal from server
  severityLabel: NotamSeverity;
  classification: number;
  fetchedAt: string;
  updatedAt: string | null;
}

export const SEVERITY_LABELS: Record<number, NotamSeverity> = {
  0: 'Unknown',
  1: 'Advisory',
  2: 'Caution',
  3: 'Warning',
  4: 'Critical',
};

import { Component, Input } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Notam } from '../../models/notam.model';

@Component({
  selector: 'app-notam-card',
  standalone: true,
  imports: [CommonModule, DatePipe],
  template: `
    <article class="rounded-lg border bg-gray-900 overflow-hidden transition-all duration-300"
             [ngClass]="borderClass">

      <!-- Severity stripe + header -->
      <div class="flex items-start gap-3 p-4">
        <span class="mt-0.5 h-3 w-3 flex-shrink-0 rounded-full"
              [ngClass]="dotClass"
              [title]="notam.severityLabel"></span>

        <div class="flex-1 min-w-0">
          <div class="flex flex-wrap items-center gap-2 mb-1">
            <span class="font-mono text-xs font-bold text-gray-300">{{ notam.notamNumber }}</span>
            <span class="px-1.5 py-0.5 rounded text-xs font-semibold uppercase tracking-wide"
                  [ngClass]="badgeClass">
              {{ notam.severityLabel }}
            </span>
            <span class="px-1.5 py-0.5 rounded bg-gray-700 text-gray-300 text-xs font-mono">
              {{ notam.icaoLocation }}
            </span>
            <span *ngIf="notam.subject" class="text-xs text-gray-400">
              {{ notam.subject }}<span *ngIf="notam.condition"> · {{ notam.condition }}</span>
            </span>
          </div>

          <!-- Effective window -->
          <div class="flex flex-wrap gap-x-4 text-xs text-gray-500 mb-2">
            <span *ngIf="notam.startValidity">
              From {{ notam.startValidity | date:'dd MMM yyyy HH:mm' : 'UTC' }} UTC
            </span>
            <span *ngIf="notam.endValidity">
              To {{ notam.endValidity | date:'dd MMM yyyy HH:mm' : 'UTC' }} UTC
            </span>
            <span *ngIf="!notam.endValidity && !notam.startValidity" class="italic">
              No validity window
            </span>
          </div>

          <!-- Free text -->
          <p class="text-sm text-gray-200 whitespace-pre-wrap leading-relaxed">{{ notam.freeText }}</p>
        </div>
      </div>

      <!-- Collapsible raw text -->
      <div class="border-t border-gray-800">
        <button
          class="w-full flex items-center justify-between px-4 py-2 text-xs text-gray-500 hover:text-gray-300 hover:bg-gray-800/50 transition-colors"
          (click)="rawOpen = !rawOpen"
          [attr.aria-expanded]="rawOpen">
          <span>Raw text</span>
          <svg class="h-3.5 w-3.5 transition-transform" [class.rotate-180]="rawOpen"
               viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd"
              d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z"
              clip-rule="evenodd" />
          </svg>
        </button>
        <div class="collapsible" [class.open]="rawOpen">
          <div>
            <pre class="px-4 py-3 text-xs font-mono text-gray-400 bg-gray-950 overflow-x-auto whitespace-pre-wrap">{{ notam.rawText }}</pre>
          </div>
        </div>
      </div>
    </article>
  `,
})
export class NotamCardComponent {
  @Input({ required: true }) notam!: Notam;

  rawOpen = false;

  get borderClass(): string {
    return {
      Unknown:  'border-gray-700',
      Advisory: 'border-blue-700',
      Caution:  'border-amber-600',
      Warning:  'border-orange-500',
      Critical: 'border-red-500',
    }[this.notam.severityLabel] ?? 'border-gray-700';
  }

  get dotClass(): string {
    return {
      Unknown:  'bg-gray-500',
      Advisory: 'bg-blue-500',
      Caution:  'bg-amber-500',
      Warning:  'bg-orange-500',
      Critical: 'bg-red-500',
    }[this.notam.severityLabel] ?? 'bg-gray-500';
  }

  get badgeClass(): string {
    return {
      Unknown:  'bg-gray-700 text-gray-300',
      Advisory: 'bg-blue-900/60 text-blue-300',
      Caution:  'bg-amber-900/60 text-amber-300',
      Warning:  'bg-orange-900/60 text-orange-300',
      Critical: 'bg-red-900/60 text-red-300',
    }[this.notam.severityLabel] ?? 'bg-gray-700 text-gray-300';
  }
}

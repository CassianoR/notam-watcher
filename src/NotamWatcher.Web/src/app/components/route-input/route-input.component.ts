import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-route-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="bg-gray-900 rounded-xl border border-gray-700 p-5">
      <h2 class="text-sm font-semibold text-gray-300 mb-3 uppercase tracking-widest">
        Watched Route
      </h2>

      <!-- Tag-style input area -->
      <div class="flex flex-wrap gap-2 mb-3 min-h-[2.5rem] p-2 rounded-lg bg-gray-800 border border-gray-700
                  focus-within:border-blue-500 focus-within:ring-1 focus-within:ring-blue-500 transition-colors"
           (click)="inputEl.focus()">
        <span *ngFor="let code of codes"
              class="inline-flex items-center gap-1 px-2 py-0.5 rounded font-mono text-xs
                     bg-blue-900/50 text-blue-200 border border-blue-700">
          {{ code }}
          <button class="ml-0.5 text-blue-400 hover:text-red-400 transition-colors leading-none"
                  (click)="remove(code); $event.stopPropagation()"
                  [attr.aria-label]="'Remove ' + code">✕</button>
        </span>

        <input #inputEl
               [(ngModel)]="draft"
               (keydown)="onKey($event)"
               (blur)="commitDraft()"
               class="flex-1 min-w-[6rem] bg-transparent outline-none text-sm text-gray-200
                      placeholder-gray-600 uppercase"
               placeholder="{{ codes.length === 0 ? 'Type ICAO code, press Enter or comma…' : 'Add more…' }}"
               maxlength="4"
               autocomplete="off"
               spellcheck="false" />
      </div>

      <p class="text-xs text-gray-600 mb-4">
        Type 4-letter ICAO codes (e.g. KJFK, KLAX) separated by Enter or comma.
      </p>

      <!-- Action buttons -->
      <div class="flex gap-2">
        <button
          class="flex-1 py-2 rounded-lg text-sm font-medium transition-colors
                 bg-blue-600 hover:bg-blue-500 text-white disabled:opacity-40 disabled:cursor-not-allowed"
          [disabled]="codes.length === 0"
          (click)="watch()">
          Watch Route
        </button>
        <button
          class="py-2 px-4 rounded-lg text-sm font-medium transition-colors
                 bg-gray-700 hover:bg-gray-600 text-gray-300 disabled:opacity-40 disabled:cursor-not-allowed"
          [disabled]="codes.length === 0"
          (click)="clear()">
          Clear
        </button>
      </div>
    </div>
  `,
})
export class RouteInputComponent {
  @Output() routeSelected = new EventEmitter<string[]>();

  codes: string[] = [];
  draft = '';

  onKey(e: KeyboardEvent): void {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      this.commitDraft();
    } else if (e.key === 'Backspace' && this.draft === '' && this.codes.length > 0) {
      this.codes.pop();
    }
  }

  commitDraft(): void {
    const code = this.draft.trim().toUpperCase().replace(/[^A-Z0-9]/g, '');
    if (code.length === 4 && !this.codes.includes(code)) {
      this.codes = [...this.codes, code];
    }
    this.draft = '';
  }

  remove(code: string): void {
    this.codes = this.codes.filter(c => c !== code);
  }

  watch(): void {
    this.commitDraft();
    if (this.codes.length > 0) {
      this.routeSelected.emit([...this.codes]);
    }
  }

  clear(): void {
    this.codes = [];
    this.draft = '';
  }
}

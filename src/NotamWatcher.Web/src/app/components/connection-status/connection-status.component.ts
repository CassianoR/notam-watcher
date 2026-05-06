import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HubStatus } from '../../services/notam-hub.service';

@Component({
  selector: 'app-connection-status',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="inline-flex items-center gap-1.5 text-xs font-medium px-2.5 py-1 rounded-full"
          [ngClass]="containerClass">
      <span class="h-2 w-2 rounded-full" [ngClass]="dotClass"></span>
      {{ label }}
    </span>
  `,
})
export class ConnectionStatusComponent {
  @Input() status: HubStatus = 'disconnected';

  get label(): string {
    return {
      disconnected: 'Disconnected',
      connecting:   'Connecting…',
      connected:    'Live',
      reconnecting: 'Reconnecting…',
      error:        'Error',
    }[this.status];
  }

  get dotClass(): string {
    return {
      disconnected: 'bg-gray-500',
      connecting:   'bg-yellow-400 animate-pulse',
      connected:    'bg-green-400 animate-pulse',
      reconnecting: 'bg-yellow-400 animate-pulse',
      error:        'bg-red-500',
    }[this.status];
  }

  get containerClass(): string {
    return {
      disconnected: 'bg-gray-800 text-gray-400',
      connecting:   'bg-yellow-900/40 text-yellow-300',
      connected:    'bg-green-900/40 text-green-300',
      reconnecting: 'bg-yellow-900/40 text-yellow-300',
      error:        'bg-red-900/40 text-red-300',
    }[this.status];
  }
}

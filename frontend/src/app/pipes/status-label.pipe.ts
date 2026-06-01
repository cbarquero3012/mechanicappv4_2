import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService, Lang } from '../services/translation.service';

@Pipe({
  name: 'statusLabel',
  pure: false,
})
export class StatusLabelPipe implements PipeTransform {
  private ts = inject(TranslationService);

  private lastStatus = '';
  private lastLang: Lang | null = null;
  private lastValue = '';

  private readonly statusKeys: Record<string, string> = {
    Pending: 'status.pending',
    'In Progress': 'status.inProgress',
    Completed: 'status.completed',
    Cancelled: 'status.cancelled',
  };

  transform(status: string): string {
    const lang = this.ts.currentLang();
    if (status !== this.lastStatus || lang !== this.lastLang) {
      this.lastStatus = status;
      this.lastLang = lang;
      const key = this.statusKeys[status];
      this.lastValue = key ? this.ts.t(key) : status;
    }
    return this.lastValue;
  }
}

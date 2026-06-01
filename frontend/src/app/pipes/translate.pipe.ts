import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService, Lang } from '../services/translation.service';

@Pipe({
  name: 'translate',
  pure: true,
})
export class TranslatePipe implements PipeTransform {
  private translationService = inject(TranslationService);

  private lastKey = '';
  private lastParams: Record<string, string | number> | undefined;
  private lastLang: Lang | null = null;
  private lastValue = '';

  transform(key: string, params?: Record<string, string | number>): string {
    const lang = this.translationService.currentLang();
    if (
      key !== this.lastKey ||
      params !== this.lastParams ||
      lang !== this.lastLang
    ) {
      this.lastKey = key;
      this.lastParams = params;
      this.lastLang = lang;
      this.lastValue = this.translationService.t(key, params);
    }
    return this.lastValue;
  }
}

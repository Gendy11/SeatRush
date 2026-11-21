import { formatNumber } from '@angular/common';
import { inject, LOCALE_ID, Pipe, PipeTransform } from '@angular/core';

/** The one place prices are formatted: 250 → "EGP 250", 87.5 → "EGP 87.50". */
@Pipe({ name: 'price' })
export class PricePipe implements PipeTransform {
  static readonly currency = 'EGP';

  private readonly locale = inject(LOCALE_ID);

  transform(amount: number | null | undefined): string {
    if (amount == null) return '';
    const digits = Number.isInteger(amount) ? '1.0-0' : '1.2-2';
    return `${PricePipe.currency} ${formatNumber(amount, this.locale, digits)}`;
  }
}

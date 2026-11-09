// Display-only formatting. The server owns all money arithmetic; the UI never rounds or sums amounts.
const LOCALE = 'en-US';

export function formatMoney(amount: number, currency?: string): string {
  if (currency) {
    try {
      return new Intl.NumberFormat(LOCALE, { style: 'currency', currency }).format(amount);
    } catch {
      // Unknown ISO code: fall through to a plain number followed by the code.
    }
  }
  const number = new Intl.NumberFormat(LOCALE, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount);
  return currency ? `${number} ${currency}` : number;
}

/** Rates arrive as fractions (0.11 = 11 %). */
export function formatRate(rate: number): string {
  return new Intl.NumberFormat(LOCALE, { style: 'percent', maximumFractionDigits: 2 }).format(rate);
}

/** `YYYY-MM-DD` → "Jan 5, 2026". Parsed as UTC so the day never shifts with the viewer's time zone. */
export function formatDate(isoDate: string): string {
  const date = new Date(`${isoDate}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return isoDate;
  return new Intl.DateTimeFormat(LOCALE, { dateStyle: 'medium', timeZone: 'UTC' }).format(date);
}

/** `YYYY-MM` → "November 2026". */
export function formatMonth(isoMonth: string): string {
  const date = new Date(`${isoMonth}-01T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return isoMonth;
  return new Intl.DateTimeFormat(LOCALE, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(date);
}

/** Today's date as `YYYY-MM-DD` in UTC: the server validates hire dates against today, UTC. */
export function todayUtc(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10);
}

/** The viewer's current month as `YYYY-MM` (local time: what the user thinks "this month" is). */
export function currentMonth(now: Date = new Date()): string {
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

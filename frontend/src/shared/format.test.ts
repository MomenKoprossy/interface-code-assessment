import { describe, expect, it } from 'vitest';
import { currentMonth, formatDate, formatMoney, formatMonth, formatRate, todayUtc } from './format';

describe('formatMoney', () => {
  it.each([
    [0, 'USD', '$0.00'],
    [0.01, 'USD', '$0.01'],
    [1234.5, 'USD', '$1,234.50'],
    [1_000_000, 'USD', '$1,000,000.00'],
    [1234.5, 'EUR', '€1,234.50'],
    [-12.3, 'USD', '-$12.30'],
  ])('%d %s → %s', (amount, currency, expected) => {
    expect(formatMoney(amount, currency)).toBe(expected);
  });

  it('always shows two decimals without a currency', () => {
    expect(formatMoney(5000)).toBe('5,000.00');
    expect(formatMoney(12.3)).toBe('12.30');
  });

  it('falls back to number + code for an invalid currency code', () => {
    expect(formatMoney(10, 'NOT-A-CODE')).toBe('10.00 NOT-A-CODE');
  });
});

describe('formatRate', () => {
  it.each([
    [0, '0%'],
    [0.11, '11%'],
    [0.125, '12.5%'],
    [1, '100%'],
  ])('%d → %s', (rate, expected) => {
    expect(formatRate(rate)).toBe(expected);
  });
});

describe('dates', () => {
  it('formats an ISO date without shifting the day', () => {
    expect(formatDate('2026-01-01')).toBe('Jan 1, 2026');
    expect(formatDate('2026-12-31')).toBe('Dec 31, 2026');
  });

  it('formats a month', () => {
    expect(formatMonth('2026-11')).toBe('November 2026');
  });

  it('returns the input when it is not a date', () => {
    expect(formatDate('nope')).toBe('nope');
  });

  it('computes today in UTC and the current local month', () => {
    expect(todayUtc(new Date('2026-11-09T23:59:59Z'))).toBe('2026-11-09');
    expect(currentMonth(new Date(2026, 0, 31))).toBe('2026-01');
    expect(currentMonth(new Date(2026, 11, 1))).toBe('2026-12');
  });
});

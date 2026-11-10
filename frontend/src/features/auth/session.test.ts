import { afterEach, describe, expect, it } from 'vitest';
import { loadSession } from './session';

describe('loadSession', () => {
  afterEach(() => sessionStorage.clear());

  it('drops an unreadable stored session instead of leaving it behind', () => {
    sessionStorage.setItem('employee-payroll.session', 'not json');

    expect(loadSession()).toBeNull();
    expect(sessionStorage.length).toBe(0);
  });
});

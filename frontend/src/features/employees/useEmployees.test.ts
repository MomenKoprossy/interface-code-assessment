import { describe, expect, it } from 'vitest';
import { parseListQuery, updateListParams } from './useEmployees';

describe('parseListQuery', () => {
  it('uses defaults for an empty URL', () => {
    expect(parseListQuery(new URLSearchParams())).toEqual({
      page: 1,
      pageSize: 10,
      search: undefined,
      department: undefined,
      status: undefined,
    });
  });

  it('reads valid params', () => {
    const query = parseListQuery(new URLSearchParams('page=3&search=%20ada%20&department=R%26D&status=Inactive'));
    expect(query).toEqual({ page: 3, pageSize: 10, search: 'ada', department: 'R&D', status: 'Inactive' });
  });

  it.each(['0', '-2', 'abc', ''])('falls back to page 1 for page=%j', (page) => {
    expect(parseListQuery(new URLSearchParams({ page })).page).toBe(1);
  });

  it('ignores an unknown status', () => {
    expect(parseListQuery(new URLSearchParams('status=Retired')).status).toBeUndefined();
  });
});

describe('updateListParams', () => {
  it('resets to page 1 when a filter changes', () => {
    const next = updateListParams(new URLSearchParams('page=4&search=ada'), { status: 'Active' });
    expect(next.toString()).toBe('search=ada&status=Active');
  });

  it('removes empty values and omits page 1', () => {
    expect(updateListParams(new URLSearchParams('search=ada&page=2'), { search: '' }).toString()).toBe('');
    expect(updateListParams(new URLSearchParams('page=2'), { page: '1' }).toString()).toBe('');
  });

  it('keeps filters when changing page', () => {
    expect(updateListParams(new URLSearchParams('department=Sales'), { page: '2' }).toString()).toBe(
      'department=Sales&page=2',
    );
  });
});

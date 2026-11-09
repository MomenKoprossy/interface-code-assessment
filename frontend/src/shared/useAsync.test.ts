import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { useAsync } from './useAsync';

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((r) => (resolve = r));
  return { promise, resolve };
}

describe('useAsync', () => {
  it('ignores a response for an old key that arrives after the current one', async () => {
    const pending: Record<string, ReturnType<typeof deferred<string>>> = { a: deferred(), b: deferred() };
    const { result, rerender } = renderHook(({ key }) => useAsync(key, () => pending[key]!.promise), {
      initialProps: { key: 'a' },
    });

    rerender({ key: 'b' });
    await act(async () => pending.b!.resolve('B'));
    await act(async () => pending.a!.resolve('A'));

    expect(result.current.data).toBe('B');
    expect(result.current.loading).toBe(false);
  });

  it('keeps the last data while the next key loads, then replaces it', async () => {
    const next = deferred<string>();
    const { result, rerender } = renderHook(
      ({ key }) => useAsync(key, () => (key === 'a' ? Promise.resolve('A') : next.promise)),
      { initialProps: { key: 'a' } },
    );
    await waitFor(() => expect(result.current.data).toBe('A'));

    rerender({ key: 'b' });
    expect(result.current).toMatchObject({ data: 'A', loading: true });

    await act(async () => next.resolve('B'));
    expect(result.current).toMatchObject({ data: 'B', loading: false });
  });

  it('returns no data for an error, even after an earlier success', async () => {
    const { result, rerender } = renderHook(
      ({ key }) => useAsync(key, () => (key === 'a' ? Promise.resolve('A') : Promise.reject(new Error('boom')))),
      { initialProps: { key: 'a' } },
    );
    await waitFor(() => expect(result.current.data).toBe('A'));

    rerender({ key: 'b' });
    await waitFor(() => expect(result.current.error).toBeInstanceOf(Error));
    expect(result.current.data).toBeUndefined();
  });
});

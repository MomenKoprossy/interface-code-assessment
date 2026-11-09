import { useCallback, useEffect, useEffectEvent, useState } from 'react';

interface Settled<T> {
  key: string;
  data?: T;
  error?: unknown;
}

export interface AsyncState<T> {
  data: T | undefined;
  error: unknown;
  loading: boolean;
  reload: () => void;
}

/**
 * Loads data whenever `key` changes (null = don't load) and aborts the previous request.
 * `loading` is derived from "no result for the current key yet", so stale responses can never win.
 * While the next result loads, `data` keeps the last successful result, so callers can keep their UI mounted
 * (no flash, no lost keyboard focus); check `loading` when stale data must not be shown.
 */
export function useAsync<T>(key: string | null, load: (signal: AbortSignal) => Promise<T>): AsyncState<T> {
  const [settled, setSettled] = useState<Settled<T> | null>(null);
  const [version, setVersion] = useState(0);
  const [last, setLast] = useState<T>();
  const runLoad = useEffectEvent(load);
  const requestKey = key === null ? null : `${key}#${version}`;

  useEffect(() => {
    if (requestKey === null) return;
    const controller = new AbortController();
    runLoad(controller.signal).then(
      (data) => {
        // A late response for an aborted request must not overwrite the current one.
        if (controller.signal.aborted) return;
        setSettled({ key: requestKey, data });
        setLast(data);
      },
      (error: unknown) => {
        if (!controller.signal.aborted) setSettled({ key: requestKey, error });
      },
    );
    return () => controller.abort();
  }, [requestKey]);

  const reload = useCallback(() => setVersion((v) => v + 1), []);
  const current = settled?.key === requestKey ? settled : null;
  const loading = requestKey !== null && current === null;
  return { data: loading ? last : current?.data, error: current?.error, loading, reload };
}

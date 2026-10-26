"use client";

import { useCallback, useEffect, useState } from "react";
import { ApiError } from "./api/http";

interface Settled<T> {
  /** Key of the request these results belong to; differs from the current key while loading. */
  key: string;
  data: T | null;
  error: ApiError | null;
}

/**
 * Minimal data-fetching hook: runs `fetcher` whenever `deps` change, exposes loading/error/data
 * and a `reload` to refresh after a mutation. While a new request is in flight the previous data
 * stays available (stale-while-loading) so forms and headers do not unmount. Stale responses are
 * ignored by comparing request keys, and state is only set from async callbacks.
 */
export function useQuery<T>(fetcher: () => Promise<T>, deps: readonly unknown[]) {
  const [version, setVersion] = useState(0);
  const requestKey = JSON.stringify([...deps, version]);
  const [settled, setSettled] = useState<Settled<T>>({ key: "", data: null, error: null });

  useEffect(() => {
    let cancelled = false;

    fetcher().then(
      (data) => {
        if (!cancelled) setSettled({ key: requestKey, data, error: null });
      },
      (error: unknown) => {
        if (cancelled) return;
        const apiError =
          error instanceof ApiError
            ? error
            : new ApiError({ status: 0, code: "Client.Unexpected", detail: "Ocurrió un error inesperado." });
        setSettled({ key: requestKey, data: null, error: apiError });
      },
    );

    return () => {
      cancelled = true;
    };
    // The fetcher closure is rebuilt on every render; the deps array is the real identity.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [requestKey]);

  const reload = useCallback(() => setVersion((v) => v + 1), []);
  const loading = settled.key !== requestKey;

  return {
    data: settled.data,
    error: loading ? null : settled.error,
    loading,
    reload,
  };
}

import { useEffect, useState } from "react";
// Discard stale responses after filter changes, unmount, logout or navigation.
export function useApiData<T>(
  load: () => Promise<T>,
  dependencies: readonly unknown[],
) {
  const [data, setData] = useState<T>();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    let active = true;
    setData(undefined);
    setLoading(true);
    setError("");
    load()
      .then((value) => {
        if (active) setData(value);
      })
      .catch((cause) => {
        if (active)
          setError(
            cause instanceof Error
              ? cause.message
              : "No se pudo conectar al servicio.",
          );
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, dependencies);
  return { data, error, loading };
}
export function LoadStatus({
  error,
  loading,
}: {
  error: string;
  loading: boolean;
}) {
  return error ? (
    <p className="form-error" role="alert">
      {error}
    </p>
  ) : loading ? (
    <p role="status">Cargando…</p>
  ) : null;
}

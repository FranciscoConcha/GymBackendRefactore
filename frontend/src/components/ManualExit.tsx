import { useRef, useState } from "react";
import { api } from "../api";
export default function ManualExit({
  userId,
  fullName,
  onExit,
}: {
  userId: number;
  fullName: string;
  onExit: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const sending = useRef(false);
  const key = useRef<string | null>(null);
  async function exit() {
    if (
      sending.current ||
      !window.confirm(`¿Marcar salida manual (sin QR) de ${fullName}?`)
    )
      return;
    sending.current = true;
    setBusy(true);
    setError("");
    key.current ??= crypto.randomUUID();
    try {
      await api.exit({ userId }, key.current);
      key.current = null;
      onExit();
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo registrar la salida.",
      );
    } finally {
      sending.current = false;
      setBusy(false);
    }
  }
  return (
    <div>
      <span>Salida manual (sin QR)</span>
      <button
        className="text-button"
        disabled={busy}
        onClick={() => void exit()}
      >
        Marcar salida
      </button>
      {error && <p role="alert">{error}</p>}
    </div>
  );
}

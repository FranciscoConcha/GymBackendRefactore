import { useEffect, useRef, useState } from "react";

export default function Camera({
  onRead,
}: {
  onRead: (value: string) => void;
}) {
  const [error, setError] = useState("");
  const callback = useRef(onRead);
  callback.current = onRead;
  useEffect(() => {
    let disposed = false;
    let scanner: import("html5-qrcode").Html5Qrcode | undefined;
    const start = async () => {
      try {
        if (!window.isSecureContext)
          throw new Error("La cámara necesita una conexión HTTPS.");
        const { Html5Qrcode } = await import("html5-qrcode");
        if (disposed) return;
        scanner = new Html5Qrcode("qr-camera");
        await scanner.start(
          { facingMode: "environment" },
          { fps: 8, qrbox: 220 },
          (value) => {
            if (!disposed) callback.current(value);
          },
          () => {},
        );
        if (disposed && scanner.isScanning) await scanner.stop();
      } catch (cause) {
        if (!disposed)
          setError(
            cause instanceof Error
              ? cause.message
              : "No pudimos abrir la cámara. Revisa los permisos o pega el código.",
          );
      }
    };
    void start();
    return () => {
      disposed = true;
      if (scanner?.isScanning) void scanner.stop().catch(() => {});
    };
  }, []);
  return (
    <div className="camera">
      <div id="qr-camera" />
      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
    </div>
  );
}

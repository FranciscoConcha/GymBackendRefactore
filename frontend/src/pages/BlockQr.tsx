import { useEffect, useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { api, type QrResponse } from "../api";
import { formatRut, qrValidity } from "../domain";
import { LoadStatus } from "../remote";
export default function BlockQr({
  id,
  group = false,
}: {
  id: number;
  group?: boolean;
}) {
  const [data, setData] = useState<QrResponse>();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    let active = true;
    let pending = false;
    let timer: ReturnType<typeof setInterval> | undefined;
    setData(undefined);
    setError("");
    setLoading(true);
    async function refresh() {
      if (pending || document.hidden) return;
      pending = true;
      try {
        const value = await api.qr(id, group);
        if (active) {
          setData(value);
          setError("");
          setNow(Date.now());
        }
      } catch (cause) {
        if (active)
          setError(
            cause instanceof Error
              ? cause.message
              : "No se pudo consultar el QR.",
          );
      } finally {
        pending = false;
        if (active) setLoading(false);
      }
    }
    function visibility() {
      clearInterval(timer);
      if (!document.hidden) {
        void refresh();
        timer = setInterval(() => void refresh(), 5000);
      }
    }
    visibility();
    document.addEventListener("visibilitychange", visibility);
    return () => {
      active = false;
      clearInterval(timer);
      document.removeEventListener("visibilitychange", visibility);
    };
  }, [id, group]);
  const title = data?.purpose === "Salida" ? "QR de salida" : "QR de entrada";
  return (
    <>
      <LoadStatus error={error} loading={loading} />
      {data && (
        <>
          <h2>{title}</h2>
          <div className="qr-person">
            <h2>{data.user.fullName}</h2>
            <strong className="qr-rut">{formatRut(data.user.rut)}</strong>
            <span>{data.user.category}</span>
            <span>{data.user.sportBranch?.name}</span>
          </div>
          <div
            className={`qr-frame ${data.purpose === "Salida" ? "qr-exit" : ""}`}
          >
            <QRCodeSVG
              value={data.token}
              size={224}
              level="M"
              marginSize={3}
              title={title}
            />
          </div>
          <p role="status">
            {data.purpose === "Salida"
              ? "Muéstralo al salir"
              : qrValidity(data.validFrom, data.validUntil, now)}
          </p>
          <p>
            Vigencia:{" "}
            <time>{new Date(data.validFrom).toLocaleString("es-CL")}</time> –{" "}
            <time>{new Date(data.validUntil).toLocaleString("es-CL")}</time>
          </p>
          <code className="ticket-code">{data.token}</code>
        </>
      )}
    </>
  );
}

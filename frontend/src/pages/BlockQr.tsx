import { useEffect, useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { api } from "../api";
import { formatRut, qrValidity } from "../domain";
import { LoadStatus, useApiData } from "../remote";
export default function BlockQr({
  id,
  group = false,
}: {
  id: number;
  group?: boolean;
}) {
  const { data, error, loading } = useApiData(
    () => api.qr(id, group),
    [id, group],
  );
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 15000);
    return () => clearInterval(timer);
  }, []);
  return (
    <>
      <LoadStatus error={error} loading={loading} />
      {data && (
        <>
          <div className="qr-person">
            <h2>{data.user.fullName}</h2>
            <strong className="qr-rut">{formatRut(data.user.rut)}</strong>
            <span>{data.user.category}</span>
            <span>{data.user.sportBranch?.name}</span>
          </div>
          <div className="qr-frame">
            <QRCodeSVG
              value={data.token}
              size={224}
              level="M"
              marginSize={3}
              title="Código QR de la reserva"
            />
          </div>
          <p role="status">
            {qrValidity(data.validFrom, data.validUntil, now)}
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

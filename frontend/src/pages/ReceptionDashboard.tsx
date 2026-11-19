import { useState } from "react";
import { ArrowRight, ScanLine, Users } from "lucide-react";
import { api } from "../api";
import { formatRut } from "../domain";
import { LoadStatus, useApiData } from "../remote";
export default function ReceptionDashboard({ onScan }: { onScan: () => void }) {
  const [revision, setRevision] = useState(0);
  const { data, error, loading } = useApiData(() => api.current(), [revision]);
  return (
    <div className="reception-dashboard">
      <LoadStatus error={error} loading={loading} />
      <section className="reception-status card">
        <span className="option-icon">
          <Users size={25} />
        </span>
        <div>
          <span className="eyebrow">AFORO ACTUAL</span>
          <strong>
            {data?.occupancy.current ?? "—"}
            <small> / {data?.occupancy.max ?? "—"}</small>
          </strong>
          {data && (
            <p>
              {Math.max(0, data.occupancy.max - data.occupancy.current)} lugares
              disponibles
            </p>
          )}
        </div>
        <button className="button" onClick={onScan}>
          <ScanLine size={18} />
          Escanear QR
          <ArrowRight size={18} />
        </button>
      </section>
      <section className="card">
        <div className="section-title">
          <h2>Personas dentro</h2>
          <button
            className="text-button"
            onClick={() => setRevision((v) => v + 1)}
          >
            Actualizar estado
          </button>
        </div>
        {data?.people.map((p) => (
          <div className="present-member" key={p.userId}>
            <span className="avatar">{p.fullName.slice(0, 1)}</span>
            <div>
              <strong>{p.fullName}</strong>
              <span>
                {formatRut(p.rut)} · {p.category}
              </span>
              <span>Fin de sesión: {p.sessionEndTime}</span>
            </div>
          </div>
        ))}
        {data?.people.length === 0 && (
          <div className="panel-empty">
            <Users size={28} />
            <h3>La sala está libre</h3>
          </div>
        )}
      </section>
    </div>
  );
}

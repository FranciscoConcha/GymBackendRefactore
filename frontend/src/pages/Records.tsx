import { useState } from "react";
import { api, type ReportFilters } from "../api";
import { CATEGORIES, shortDate } from "../domain";
import { LoadStatus, useApiData } from "../remote";
export function ReportFilterFields({
  value,
  onChange,
}: {
  value: ReportFilters;
  onChange: (f: ReportFilters) => void;
}) {
  const branches = useApiData(() => api.branches(), []);
  return (
    <div className="form-grid">
      <label className="field">
        Desde
        <input
          type="date"
          value={value.from ?? ""}
          onChange={(e) => onChange({ ...value, from: e.target.value })}
        />
      </label>
      <label className="field">
        Hasta
        <input
          type="date"
          value={value.to ?? ""}
          onChange={(e) => onChange({ ...value, to: e.target.value })}
        />
      </label>
      <label className="field">
        Rama deportiva
        <select
          value={value.branchId ?? ""}
          onChange={(e) =>
            onChange({
              ...value,
              branchId: e.target.value ? Number(e.target.value) : undefined,
            })
          }
        >
          <option value="">Todas las ramas</option>
          {branches.data?.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </label>
      <label className="field">
        Categoría
        <select
          value={value.category ?? ""}
          onChange={(e) => onChange({ ...value, category: e.target.value })}
        >
          <option value="">Todas las categorías</option>
          {CATEGORIES.map((c) => (
            <option key={c}>{c}</option>
          ))}
        </select>
      </label>
      <LoadStatus error={branches.error} loading={branches.loading} />
    </div>
  );
}
export function BranchesPanel({ onChange }: { onChange: () => void }) {
  const [revision, setRevision] = useState(0);
  const [name, setName] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const branches = useApiData(() => api.branches(), [revision]);
  async function mutate(operation: () => Promise<unknown>) {
    setBusy(true);
    setError("");
    try {
      await operation();
      setRevision((v) => v + 1);
      onChange();
    } catch (cause) {
      setError(
        cause instanceof Error ? cause.message : "No se pudo guardar la rama.",
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="card">
      <h2>Ramas deportivas</h2>
      <LoadStatus error={branches.error} loading={branches.loading} />
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void mutate(async () => {
            await api.createBranch(name);
            setName("");
          });
        }}
      >
        <label className="field">
          Nombre de la rama
          <input
            required
            maxLength={80}
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </label>
        <button className="button" disabled={busy}>
          Crear rama
        </button>
      </form>
      {branches.data?.map((b) => (
        <form
          className="group-record"
          key={`${b.id}-${b.name}`}
          onSubmit={(e) => {
            e.preventDefault();
            const data = new FormData(e.currentTarget);
            void mutate(() =>
              api.updateBranch(b.id, { name: String(data.get("name")) }),
            );
          }}
        >
          <label className="field">
            Nombre de {b.name}
            <input name="name" defaultValue={b.name} required maxLength={80} />
          </label>
          <button className="text-button" disabled={busy}>
            Guardar nombre
          </button>
          <button
            type="button"
            className="text-button"
            disabled={busy}
            onClick={() =>
              void mutate(() => api.updateBranch(b.id, { enabled: !b.enabled }))
            }
          >
            {b.enabled ? "Deshabilitar" : "Habilitar"} rama
          </button>
        </form>
      ))}
      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
    </section>
  );
}
export function HistoryPanel({ admin }: { admin: boolean }) {
  const [filters, setFilters] = useState<ReportFilters>({});
  const [userId, setUserId] = useState("");
  const result = useApiData(
    () =>
      admin
        ? api.visits({
            ...filters,
            userId: userId ? Number(userId) : undefined,
          })
        : api.myVisits(),
    [admin, filters, userId],
  );
  return (
    <section className="card">
      <h2>{admin ? "Visitas" : "Mis visitas"}</h2>
      {admin && (
        <>
          <ReportFilterFields value={filters} onChange={setFilters} />
          <label className="field">
            ID de usuario
            <input
              type="number"
              min={1}
              value={userId}
              onChange={(e) => setUserId(e.target.value)}
            />
          </label>
        </>
      )}
      <LoadStatus error={result.error} loading={result.loading} />
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Usuario</th>
              <th>Entrada</th>
              <th>Salida</th>
              <th>Permanencia</th>
              <th>Documento</th>
              <th>Operadores</th>
            </tr>
          </thead>
          <tbody>
            {result.data?.map((v) => (
              <tr key={v.id}>
                <td>
                  {v.fullName}
                  <small>{v.rut}</small>
                </td>
                <td>{new Date(v.entryAt).toLocaleString("es-CL")}</td>
                <td>
                  {v.exitAt
                    ? new Date(v.exitAt).toLocaleString("es-CL")
                    : "En sala"}
                </td>
                <td>
                  {v.stayMinutes === null
                    ? "—"
                    : `${Math.round(v.stayMinutes)} min`}
                </td>
                <td>{v.documentType}</td>
                <td>
                  {v.entryOperator} / {v.exitOperator ?? "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {result.data?.length === 0 && <p>Sin visitas en este período.</p>}
    </section>
  );
}
export function AlertsPanel() {
  const [filters, setFilters] = useState<ReportFilters>({});
  const result = useApiData(() => api.adminAlerts(filters), [filters]);
  return (
    <section className="card">
      <h2>Inasistencias</h2>
      <div className="form-grid">
        <label className="field">
          Desde
          <input
            type="date"
            value={filters.from ?? ""}
            onChange={(e) => setFilters({ ...filters, from: e.target.value })}
          />
        </label>
        <label className="field">
          Hasta
          <input
            type="date"
            value={filters.to ?? ""}
            onChange={(e) => setFilters({ ...filters, to: e.target.value })}
          />
        </label>
      </div>
      <LoadStatus error={result.error} loading={result.loading} />
      <ul className="attendance-alerts">
        {result.data?.map((a) => (
          <li key={a.id}>
            Reserva {a.reservationId} · {shortDate(a.date)} · {a.startTime} a{" "}
            {a.endTime} · {a.acknowledgedAt ? "Leída" : "Pendiente de lectura"}
          </li>
        ))}
      </ul>
      {result.data?.length === 0 && <p>Sin inasistencias en este período.</p>}
    </section>
  );
}

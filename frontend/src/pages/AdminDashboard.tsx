import { lazy, Suspense, useEffect, useState } from "react";
import {
  CalendarDays,
  ScanLine,
  Users,
  Clock3,
  Activity,
  type LucideIcon,
} from "lucide-react";
import { api, type ReportFilters, type Occupancy } from "../api";
import { localDate } from "../domain";
import { LoadStatus, useApiData } from "../remote";
import { ReportFilterFields } from "./Records";
import { attendanceRate } from "../metrics";
const OccupancyChart = lazy(() =>
  import("../components/admin-charts").then((m) => ({
    default: m.OccupancyChart,
  })),
);
const DemandChart = lazy(() =>
  import("../components/admin-charts").then((m) => ({
    default: m.DemandChart,
  })),
);
function Metric({
  label,
  value,
  detail,
  icon: Icon = CalendarDays,
  accent = false,
}: {
  label: string;
  value: string | number;
  detail?: string;
  icon?: LucideIcon;
  accent?: boolean;
}) {
  return (
    <article className={`metric-card ${accent ? "metric-dark" : ""}`}>
      <div className="metric-top">
        <span>{label}</span>
        <span className="metric-icon">
          <Icon size={19} aria-hidden="true" />
        </span>
      </div>
      <strong className="metric-value">{value}</strong>
      <p>{detail}</p>
    </article>
  );
}
export default function AdminDashboard({
  onAccess,
  onReservations,
  onUsers,
}: {
  onAccess: () => void;
  onReservations: () => void;
  onUsers: () => void;
}) {
  const [filters, setFilters] = useState<ReportFilters>({});
  const [revision, setRevision] = useState(0);
  const {
    data: m,
    error,
    loading,
  } = useApiData(() => api.metrics(filters), [filters, revision]);
  const [lastOccupancy, setLastOccupancy] = useState<Occupancy>();
  useEffect(() => {
    if (m) setLastOccupancy(m.occupancy);
  }, [m]);
  function period(days: number) {
    const now = new Date();
    const to = localDate(now);
    const fromDate = new Date(`${to}T12:00:00Z`);
    fromDate.setUTCDate(fromDate.getUTCDate() - days + 1);
    setFilters({ ...filters, from: localDate(fromDate), to });
  }
  return (
    <div className="admin-dashboard">
      <div className="dashboard-controls">
        <ReportFilterFields value={filters} onChange={setFilters} />
        <div className="period-control" aria-label="Período del panel">
          {[1, 7, 30].map((d) => (
            <button key={d} onClick={() => period(d)}>
              {d === 1 ? "Hoy" : `${d} días`}
            </button>
          ))}
        </div>
        <button
          className="text-button"
          onClick={() => setRevision((v) => v + 1)}
        >
          Actualizar resumen
        </button>
      </div>
      <LoadStatus error={error} loading={loading} />
      {!m && lastOccupancy && (
        <div className="metrics-grid">
          <Metric
            label="Aforo actual"
            value={`${lastOccupancy.current} / ${lastOccupancy.max}`}
            detail="Última consulta global de la sala"
          />
        </div>
      )}
      {m && (
        <>
          <p>
            {m.period.from} – {m.period.to}
          </p>
          <div className="metrics-grid">
            <Metric
              label="Reservas"
              value={m.reservations.total}
              detail={`${m.reservations.attended} asistidas · ${m.reservations.cancelled} canceladas`}
            />
            <Metric
              label="Aforo actual"
              icon={Users}
              accent
              value={`${m.occupancy.current} / ${m.occupancy.max}`}
              detail="Ocupación global de la sala"
            />
            <Metric label="Inasistencias" value={m.reservations.noShow} />
            <Metric
              label="Visitas"
              icon={Activity}
              value={m.visits}
              detail={`${m.uniqueVisitors} personas distintas`}
            />
            <Metric
              label="Permanencia media"
              icon={Clock3}
              value={
                m.averageStayMinutes === null
                  ? "Sin datos"
                  : `${Math.round(m.averageStayMinutes)} min`
              }
            />
            <Metric
              label="Asistencia"
              icon={Activity}
              value={
                attendanceRate(m) === null
                  ? "Sin datos"
                  : `${attendanceRate(m)}%`
              }
            />
            <Metric
              label="Sesiones de rama"
              icon={Users}
              value={m.groupBookings.count}
              detail={`${m.groupBookings.usedSeats} de ${m.groupBookings.seats} cupos usados`}
            />
          </div>
          <div className="admin-main-grid">
            <section className="card">
              <h2>Aforo actual</h2>
              <Suspense fallback={<p role="status">Cargando gráfico…</p>}>
                <OccupancyChart
                  present={m.occupancy.current}
                  capacity={m.occupancy.max}
                />
              </Suspense>
            </section>
            <section className="card">
              <h2>Demanda por horario</h2>
              <Suspense fallback={<p role="status">Cargando gráfico…</p>}>
                <DemandChart
                  demand={m.demandByHour.map((d) => ({
                    start: d.startTime,
                    count: d.reservations,
                  }))}
                />
              </Suspense>
            </section>
          </div>
          <div className="admin-lower-grid">
            <section className="card">
              <h2>Demanda por rama</h2>
              <table>
                <thead>
                  <tr>
                    <th>Rama</th>
                    <th>Reservas</th>
                  </tr>
                </thead>
                <tbody>
                  {m.demandByBranch.map((d) => (
                    <tr key={d.branchId}>
                      <td>{d.name}</td>
                      <td>{d.reservations}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
            <section className="card">
              <h2>Usuarios habilitados por categoría</h2>
              <table>
                <thead>
                  <tr>
                    <th>Categoría</th>
                    <th>Usuarios</th>
                  </tr>
                </thead>
                <tbody>
                  {m.enabledUsersByCategory.map((c) => (
                    <tr key={c.category}>
                      <td>{c.category}</td>
                      <td>{c.count}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          </div>
        </>
      )}
      <div className="quick-stats">
        <button className="card" onClick={onAccess}>
          <ScanLine />
          Control de acceso
        </button>
        <button className="card" onClick={onReservations}>
          <CalendarDays />
          Reservas
        </button>
        <button className="card" onClick={onUsers}>
          <Users />
          Usuarios
        </button>
      </div>
    </div>
  );
}

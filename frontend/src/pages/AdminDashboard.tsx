import { lazy, Suspense, useState } from "react";
import {
  Activity,
  ArrowDownLeft,
  ArrowRight,
  ArrowUpRight,
  CalendarDays,
  CheckCircle2,
  Clock3,
  ScanLine,
  Users,
  type LucideIcon,
} from "lucide-react";
import { localDate, shortDate, type DemoState } from "../domain";
import { dashboardMetrics, ROOM_CAPACITY, type Period } from "../metrics";

const ActivityChart = lazy(() =>
  import("../components/admin-charts").then((module) => ({
    default: module.ActivityChart,
  })),
);
const OccupancyChart = lazy(() =>
  import("../components/admin-charts").then((module) => ({
    default: module.OccupancyChart,
  })),
);
const DemandChart = lazy(() =>
  import("../components/admin-charts").then((module) => ({
    default: module.DemandChart,
  })),
);

function ChartLoading({ className }: { className: string }) {
  return (
    <div className={`chart-loading ${className}`} role="status">
      Cargando gráfico…
    </div>
  );
}

interface Props {
  state: DemoState;
  onAccess: () => void;
  onReservations: () => void;
  onUsers: () => void;
}

function Metric({
  label,
  value,
  detail,
  icon: Icon,
  accent = false,
}: {
  label: string;
  value: string;
  detail: string;
  icon: LucideIcon;
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
  state,
  onAccess,
  onReservations,
  onUsers,
}: Props) {
  const [period, setPeriod] = useState<Period>(7);
  const metrics = dashboardMetrics(state, period);
  const events = state.reservations
    .flatMap((r) => {
      const name =
        state.members.find((m) => m.id === r.memberId)?.name ?? "Usuario";
      return [
        ...(r.entry
          ? [{ id: `${r.id}-entry`, type: "Entrada", time: r.entry, name }]
          : []),
        ...(r.exit
          ? [{ id: `${r.id}-exit`, type: "Salida", time: r.exit, name }]
          : []),
      ];
    })
    .filter(
      (event) =>
        Number.isFinite(Date.parse(event.time)) &&
        metrics.days.includes(localDate(new Date(event.time))),
    )
    .sort((a, b) => Date.parse(b.time) - Date.parse(a.time))
    .slice(0, 5);
  const periodLabel = period === 1 ? "Hoy" : `Últimos ${period} días`;

  return (
    <div className="admin-dashboard">
      <div className="dashboard-controls">
        <div className="range-caption">
          <CalendarDays size={17} aria-hidden="true" />
          <span>{periodLabel}</span>
          <span className="range-dates">
            {shortDate(metrics.days[0], { day: "numeric", month: "short" })}
            {period > 1 &&
              ` – ${shortDate(metrics.days.at(-1)!, { day: "numeric", month: "short" })}`}
          </span>
        </div>
        <div className="period-control" aria-label="Período del panel">
          {([1, 7, 30] as Period[]).map((value) => (
            <button
              key={value}
              aria-pressed={period === value}
              onClick={() => setPeriod(value)}
            >
              {value === 1 ? "Hoy" : `${value} días`}
            </button>
          ))}
        </div>
      </div>
      <div className="metrics-grid">
        <Metric
          label="Reservas"
          value={String(metrics.records.length)}
          detail={`${metrics.pending} pendientes · ${metrics.cancelled.length} canceladas`}
          icon={CalendarDays}
        />
        <Metric
          label="En sala ahora"
          value={`${metrics.present.length}`}
          detail={`Capacidad: ${ROOM_CAPACITY} personas`}
          icon={Users}
          accent
        />
        <Metric
          label="Asistencia registrada"
          value={
            metrics.attendanceRate === null ? "—" : `${metrics.attendanceRate}%`
          }
          detail={`${metrics.arrivals.length} ingresos · ${metrics.valid.length} reservas vigentes`}
          icon={CheckCircle2}
        />
        <Metric
          label="Usuarios habilitados"
          value={String(metrics.enabled)}
          detail={`${state.members.length} usuarios registrados · Estado actual`}
          icon={Activity}
        />
      </div>
      <div className="admin-main-grid">
        <section className="panel reservation-chart">
          <div className="panel-heading">
            <div>
              <span className="eyebrow">RESERVAS POR FECHA DE SESIÓN</span>
              <h2>Actividad de la sala</h2>
            </div>
            <button className="text-button" onClick={onReservations}>
              Ver reservas
              <ArrowUpRight size={16} />
            </button>
          </div>
          <Suspense fallback={<ChartLoading className="activity-echart" />}>
            <ActivityChart key={period} days={metrics.daily} />
          </Suspense>
          <div className="chart-summary">
            <span>
              <strong>{metrics.records.length}</strong> reservas en el período
            </span>
            <span>
              <strong>
                {metrics.cancellationRate === null
                  ? "—"
                  : `${metrics.cancellationRate}%`}
              </strong>{" "}
              de cancelación
            </span>
          </div>
        </section>
        <section className="panel room-panel">
          <div className="panel-heading">
            <div>
              <span className="eyebrow">CONTROL DE ACCESO</span>
              <h2>Sala de pesas</h2>
            </div>
            <span className="live-label">
              <span />
              Ahora
            </span>
          </div>
          <div className="room-visual">
            <Suspense fallback={<ChartLoading className="occupancy-echart" />}>
              <OccupancyChart
                present={metrics.present.length}
                capacity={ROOM_CAPACITY}
              />
            </Suspense>
          </div>
          <div className="room-capacity">
            <span>Capacidad</span>
            <strong>{ROOM_CAPACITY} personas</strong>
          </div>
          <button className="button" onClick={onAccess}>
            <ScanLine size={18} />
            Abrir control de acceso
            <ArrowRight size={17} />
          </button>
        </section>
      </div>
      <div className="admin-lower-grid">
        <section className="panel demand-panel">
          <div className="panel-heading">
            <div>
              <span className="eyebrow">DISTRIBUCIÓN DEL PERÍODO</span>
              <h2>Horarios más solicitados</h2>
            </div>
            <Clock3 size={19} aria-hidden="true" />
          </div>
          {metrics.demand.length ? (
            <Suspense fallback={<ChartLoading className="demand-echart" />}>
              <DemandChart demand={metrics.demand} />
            </Suspense>
          ) : (
            <div className="panel-empty">
              <Clock3 size={28} />
              <h3>Aún no hay demanda registrada</h3>
              <p>
                Los horarios aparecerán cuando haya reservas en este período.
              </p>
            </div>
          )}
        </section>
        <section className="panel movements-panel">
          <div className="panel-heading">
            <div>
              <span className="eyebrow">ÚLTIMOS MOVIMIENTOS</span>
              <h2>Entradas y salidas</h2>
            </div>
            <button
              className="text-button"
              onClick={onAccess}
              aria-label="Abrir entradas y salidas"
            >
              <ArrowUpRight size={20} />
            </button>
          </div>
          {events.length ? (
            <div className="movement-list">
              {events.map((event) => (
                <div className="movement" key={event.id}>
                  <span
                    className={`movement-icon ${event.type === "Salida" ? "exit" : ""}`}
                  >
                    {event.type === "Entrada" ? (
                      <ArrowDownLeft size={18} />
                    ) : (
                      <ArrowUpRight size={18} />
                    )}
                  </span>
                  <div>
                    <strong>{event.name}</strong>
                    <span>
                      {event.type} ·{" "}
                      {new Intl.DateTimeFormat("es-CL", {
                        day: "numeric",
                        month: "short",
                      }).format(new Date(event.time))}
                    </span>
                  </div>
                  <time dateTime={event.time}>
                    {new Date(event.time).toLocaleTimeString("es-CL", {
                      hour: "2-digit",
                      minute: "2-digit",
                    })}
                  </time>
                </div>
              ))}
            </div>
          ) : (
            <div className="panel-empty">
              <ScanLine size={28} />
              <h3>Sin movimientos recientes</h3>
              <p>Las entradas y salidas se registrarán aquí.</p>
            </div>
          )}
          <div className="panel-bottom">
            <span>
              {metrics.averageMinutes === null
                ? "Sin visitas completadas en el período"
                : `${metrics.averageMinutes} min de permanencia media en el período`}
            </span>
          </div>
        </section>
      </div>
      <button className="admin-users-link" onClick={onUsers}>
        <span className="admin-users-icon">
          <Users size={20} />
        </span>
        <span>
          <strong>Comunidad UCN</strong>
          <small>
            {metrics.enabled} habilitados de {state.members.length} usuarios
          </small>
        </span>
        <span>
          Gestionar usuarios
          <ArrowRight size={17} />
        </span>
      </button>
    </div>
  );
}

import {
  ArrowRight,
  ArrowUpRight,
  CalendarDays,
  CheckCircle2,
  Clock3,
  MapPin,
  QrCode,
  ShieldCheck,
  type LucideIcon,
} from "lucide-react";
import { Badge, Button } from "../components/ui";
import { getDays, shortDate, type Reservation } from "../domain";

interface Props {
  upcoming: Reservation[];
  reservations: Reservation[];
  onReserve: (date?: string) => void;
  onQr: () => void;
}

function Stat({
  icon: Icon,
  label,
  value,
  suffix,
}: {
  icon: LucideIcon;
  label: string;
  value: string;
  suffix?: string;
}) {
  return (
    <div className="stat">
      <span className="stat-icon">
        <Icon size={22} aria-hidden="true" />
      </span>
      <div>
        <span className="stat-label">{label}</span>
        <strong>
          {value}
          {suffix && <small>{suffix}</small>}
        </strong>
      </div>
    </div>
  );
}

export default function StudentDashboard({
  upcoming,
  reservations,
  onReserve,
  onQr,
}: Props) {
  const next = upcoming[0];
  return (
    <>
      <div className="student-home">
        <section className="hero-card">
          <div className="hero-copy">
            <span className="hero-label">
              <span />
              RESERVAS
            </span>
            <h2>
              Reserva tu
              <br />
              próxima sesión.
            </h2>
            <p>Elige entre los bloques disponibles.</p>
            <button className="button gold-button" onClick={() => onReserve()}>
              Reservar un horario
              <ArrowRight size={18} />
            </button>
            <div className="hero-location">
              <MapPin size={15} />
              Sala de pesas UCN
            </div>
          </div>
        </section>
        <section className="card next-card">
          <div className="section-title">
            <h2>Tu próxima sesión</h2>
            <CalendarDays size={19} aria-hidden="true" />
          </div>
          {next ? (
            <>
              <div className="next-date">
                <span>{shortDate(next.slot.date, { day: "2-digit" })}</span>
                <div>
                  {shortDate(next.slot.date, { month: "long" })}
                  <strong>
                    {shortDate(next.slot.date, { weekday: "long" })}
                  </strong>
                </div>
              </div>
              <div className="next-time">
                {next.slot.start}
                <span>—</span>
                {next.slot.end}
              </div>
              <p className="meta">
                <MapPin size={16} />
                Sala de pesas
              </p>
              <Badge tone={next.status === "En sala" ? "green" : "blue"}>
                {next.status}
              </Badge>
              <Button secondary onClick={onQr}>
                <QrCode size={18} />
                Ver mi código QR
              </Button>
            </>
          ) : (
            <div className="next-empty">
              <div className="calendar-illustration">
                <CalendarDays size={34} />
              </div>
              <h3>Tu agenda está libre</h3>
              <p>Elige un horario para tu próxima sesión.</p>
              <button className="text-button" onClick={() => onReserve()}>
                Buscar un bloque
                <ArrowRight size={16} />
              </button>
            </div>
          )}
        </section>
      </div>
      <div className="quick-stats">
        <Stat
          icon={CalendarDays}
          label="Reservas pendientes"
          value={String(
            upcoming.filter((r) => r.status === "Reservada").length,
          ).padStart(2, "0")}
        />
        <Stat
          icon={CheckCircle2}
          label="Sesiones completadas"
          value={String(
            reservations.filter((r) => r.status === "Completada").length,
          ).padStart(2, "0")}
        />
        <Stat icon={Clock3} label="Por sesión" value="90" suffix="min" />
      </div>
      <div className="section-heading">
        <div>
          <span className="eyebrow">PLANIFICA TU SEMANA</span>
          <h2>¿Cuándo entrenamos?</h2>
        </div>
        <button className="text-button" onClick={() => onReserve()}>
          Ver horarios
          <ArrowRight size={17} />
        </button>
      </div>
      <div className="home-days">
        {getDays().map((day) => (
          <button key={day} className="home-day" onClick={() => onReserve(day)}>
            <span>{shortDate(day, { weekday: "short" }).replace(".", "")}</span>
            <strong>{shortDate(day, { day: "2-digit" })}</strong>
            <span className="day-availability">
              <span />
              Disponible
            </span>
            <ArrowUpRight size={16} />
          </button>
        ))}
      </div>
      <div className="home-bottom">
        <ShieldCheck size={22} />
        <p>
          Tu QR está en{" "}
          <button className="inline-link" onClick={onQr}>
            Mi código QR
          </button>
          . Preséntalo al entrar y al salir.
        </p>
      </div>
    </>
  );
}

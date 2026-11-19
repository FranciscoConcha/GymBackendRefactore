import {
  lazy,
  Suspense,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from "react";
import {
  ArrowDownLeft,
  ArrowRight,
  ArrowUpRight,
  CalendarDays,
  Check,
  CheckCircle2,
  ChevronRight,
  Clock3,
  Dumbbell,
  History,
  LayoutDashboard,
  LogOut,
  MapPin,
  Menu,
  Plus,
  QrCode,
  ScanLine,
  Search,
  ShieldCheck,
  Users,
  UserRound,
  X,
  type LucideIcon,
} from "lucide-react";
import { QRCodeSVG } from "qrcode.react";
import { useLocation, useNavigate } from "react-router-dom";
import AdminDashboard from "./pages/AdminDashboard";
import StudentDashboard from "./pages/StudentDashboard";
import { Badge, Button } from "./components/ui";
import {
  access,
  cancel,
  DEMO_MEMBER,
  getDays,
  loadDemo,
  remaining,
  reserve,
  shortDate,
  slotsFor,
  STORAGE_KEY,
  ticket,
  type DemoState,
  type Member,
  type Reservation,
  type Role,
  type Slot,
} from "./domain";

const Camera = lazy(() => import("./Camera"));
const studentNav: [string, string, LucideIcon][] = [
  ["inicio", "Inicio", LayoutDashboard],
  ["horarios", "Reservar", CalendarDays],
  ["reservas", "Mis reservas", History],
  ["qr", "Mi código QR", QrCode],
];
const adminNav: [string, string, LucideIcon][] = [
  ["inicio", "Resumen", LayoutDashboard],
  ["acceso", "Control de acceso", ScanLine],
  ["usuarios", "Usuarios", Users],
  ["reservas", "Reservas", CalendarDays],
];
const statusClass: Record<string, string> = {
  Reservada: "blue",
  "En sala": "green",
  Completada: "neutral",
  Cancelada: "red",
};

function Brand({ inverse = false }: { inverse?: boolean }) {
  return (
    <div className={`brand ${inverse ? "inverse" : ""}`}>
      <img src="/deportes-ucn.png" width="64" height="64" alt="Deportes UCN" />
      <span className="brand-caption">
        <strong>UCN Gym</strong>
        <span>Sala de pesas</span>
      </span>
    </div>
  );
}

function Empty({
  title,
  children,
  action,
}: {
  title: string;
  children: ReactNode;
  action?: ReactNode;
}) {
  return (
    <div className="empty">
      <CalendarDays size={30} aria-hidden="true" />
      <h3>{title}</h3>
      <p>{children}</p>
      {action}
    </div>
  );
}
function Modal({
  title,
  children,
  onClose,
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = ref.current!;
    dialog.showModal();
    return () => dialog.close();
  }, []);
  return (
    <dialog
      ref={ref}
      className="modal"
      aria-labelledby="modal-title"
      onCancel={onClose}
    >
      <div className="modal-heading">
        <h2 id="modal-title">{title}</h2>
        <button className="icon-button" aria-label="Cerrar" onClick={onClose}>
          <X />
        </button>
      </div>
      {children}
    </dialog>
  );
}
function SlotDetails({ slot }: { slot: Slot }) {
  return (
    <>
      <span className="eyebrow">Sala de pesas</span>
      <h3>{shortDate(slot.date)}</h3>
      <p className="meta">
        <Clock3 size={16} aria-hidden="true" />
        {slot.start} a {slot.end}
        <span className="separator" />
        90 minutos
      </p>
    </>
  );
}

export default function App() {
  const location = useLocation();
  const navigate = useNavigate();
  const segments = location.pathname.split("/").filter(Boolean);
  const role: Role | null =
    segments[0] === "admin"
      ? "admin"
      : segments[0] === "estudiante"
        ? "student"
        : null;
  const [state, setState] = useState(loadDemo);
  const page = segments[1] || "inicio";
  const [menu, setMenu] = useState(false);
  const [mobile, setMobile] = useState(
    () => matchMedia("(max-width: 760px)").matches,
  );
  const drawer = useRef<HTMLElement>(null);
  const menuButton = useRef<HTMLButtonElement>(null);
  const [notice, setNotice] = useState("");
  const [failure, setFailure] = useState(false);
  const [selected, setSelected] = useState<Slot | null>(null);
  const [toCancel, setToCancel] = useState<Reservation | null>(null);
  const [showMember, setShowMember] = useState(false);
  const [scheduleDate, setScheduleDate] = useState(() => getDays()[0]);
  const heading = useRef<HTMLHeadingElement>(null);
  const member = state.members.find((m) => m.id === DEMO_MEMBER)!;
  const nav = role === "admin" ? adminNav : studentNav;
  const currentPage =
    nav.some((n) => n[0] === page) || page === "perfil" ? page : "inicio";
  const myReservations = state.reservations.filter(
    (r) => r.memberId === DEMO_MEMBER,
  );
  const upcoming = [...myReservations]
    .filter((r) => ["Reservada", "En sala"].includes(r.status))
    .sort((a, b) =>
      `${a.slot.date}${a.slot.start}`.localeCompare(
        `${b.slot.date}${b.slot.start}`,
      ),
    );

  useEffect(() => {
    const media = matchMedia("(max-width: 760px)");
    const sync = () => {
      setMobile(media.matches);
      setMenu(false);
    };
    media.addEventListener("change", sync);
    return () => media.removeEventListener("change", sync);
  }, []);
  useEffect(() => {
    if (!menu || !mobile) return;
    const panel = drawer.current!;
    const controls = () => [
      ...panel.querySelectorAll<HTMLElement>("a, button:not(:disabled)"),
    ];
    controls()[0]?.focus();
    const trap = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        closeMenu();
      }
      if (event.key !== "Tab") return;
      const elements = controls();
      const first = elements[0];
      const last = elements[elements.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    };
    panel.addEventListener("keydown", trap);
    return () => panel.removeEventListener("keydown", trap);
  }, [menu, mobile]);

  useLayoutEffect(() => {
    setMenu(false);
  }, [location.pathname]);
  useLayoutEffect(() => {
    heading.current?.focus();
  }, [currentPage, role]);
  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    } catch {
      setFailure(true);
      setNotice("No se pudieron guardar los cambios en este navegador.");
    }
  }, [state]);
  useEffect(() => {
    const sync = (event: StorageEvent) => {
      if (event.key === STORAGE_KEY) setState(loadDemo());
    };
    addEventListener("storage", sync);
    return () => removeEventListener("storage", sync);
  }, []);
  useEffect(() => {
    if (!notice) return;
    const timeout = setTimeout(() => setNotice(""), 6500);
    return () => clearTimeout(timeout);
  }, [notice]);

  function go(destination: string) {
    navigate(`/${role === "admin" ? "admin" : "estudiante"}/${destination}`);
    setMenu(false);
  }
  function closeMenu() {
    setMenu(false);
    requestAnimationFrame(() => menuButton.current?.focus());
  }
  function notify(text: string, error = false) {
    setFailure(error);
    setNotice(text);
  }
  function update(
    operation: (value: DemoState) => DemoState,
    success: string,
  ): boolean {
    try {
      setState(operation(state));
      notify(success);
      return true;
    } catch (error) {
      notify(
        error instanceof Error
          ? error.message
          : "No se pudo completar la acción.",
        true,
      );
      return false;
    }
  }
  function enter(nextRole: Role) {
    navigate(`/${nextRole === "admin" ? "admin" : "estudiante"}/inicio`);
  }

  if (!role)
    return (
      <div className="login-page">
        <section className="login-story" aria-label="Deportes UCN">
          <Brand inverse />
          <div className="login-copy">
            <span className="eyebrow">DEPORTES UCN</span>
            <h1>
              Reserva tu
              <br />
              <span>próxima sesión.</span>
            </h1>
            <p>Sala de pesas · Deportes UCN</p>
            <div className="story-line" />
            <span className="story-bottom">
              <Dumbbell size={20} aria-hidden="true" /> Sala de pesas ·
              Universidad Católica del Norte
            </span>
          </div>
        </section>
        <main className="login-main">
          <div className="login-form">
            <span className="eyebrow">UNIVERSIDAD CATÓLICA DEL NORTE</span>
            <h2>Accede a tu espacio</h2>
            <p className="intro">Selecciona tu portal.</p>
            <button className="profile-option" onClick={() => enter("student")}>
              <span className="option-icon">
                <UserRound />
              </span>
              <span>
                <strong>Estudiante</strong>
                <small>Reservas y código de acceso</small>
              </span>
              <ArrowRight size={20} />
            </button>
            <button className="profile-option" onClick={() => enter("admin")}>
              <span className="option-icon">
                <ShieldCheck />
              </span>
              <span>
                <strong>Administrador</strong>
                <small>Usuarios y control de acceso</small>
              </span>
              <ArrowRight size={20} />
            </button>
          </div>
          <span className="login-footer">Deportes UCN · Sala de pesas</span>
        </main>
      </div>
    );

  return (
    <div className="app-shell">
      <a
        className="skip-link"
        href="#main-content"
        onClick={(event) => {
          event.preventDefault();
          document.getElementById("main-content")?.focus();
        }}
      >
        Saltar al contenido
      </a>
      {menu && (
        <button
          className="nav-scrim"
          aria-label="Cerrar menú"
          onClick={closeMenu}
        />
      )}
      <aside
        ref={drawer}
        className={`sidebar ${menu ? "open" : ""}`}
        inert={mobile && !menu}
        role={mobile && menu ? "dialog" : undefined}
        aria-modal={mobile && menu ? true : undefined}
        aria-label="Menú principal"
      >
        <button
          className="icon-button drawer-close"
          aria-label="Cerrar menú"
          onClick={closeMenu}
        >
          <X />
        </button>
        <Brand inverse />
        <div className="sidebar-section">
          {role === "admin" ? "ADMINISTRACIÓN" : "MI ESPACIO"}
        </div>
        <nav aria-label="Navegación principal">
          {nav.map(([id, label, Icon]) => (
            <a
              key={id}
              href={`/${role === "admin" ? "admin" : "estudiante"}/${id}`}
              onClick={(event) => {
                event.preventDefault();
                go(id);
              }}
              className={currentPage === id ? "nav-link active" : "nav-link"}
              aria-current={currentPage === id ? "page" : undefined}
            >
              <Icon size={20} aria-hidden="true" />
              {label}
              {currentPage === id && <span className="nav-active-dot" />}
            </a>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <button className="sidebar-profile" onClick={() => go("perfil")}>
            <span className="avatar">{role === "admin" ? "RC" : "AM"}</span>
            <span>
              <strong>{role === "admin" ? "Recepción" : member.name}</strong>
              <small>
                {role === "admin" ? "Administrador" : member.category}
              </small>
            </span>
            <ChevronRight size={16} />
          </button>
          <button
            className="logout"
            onClick={() => {
              navigate("/");
              setMenu(false);
            }}
          >
            <LogOut size={17} aria-hidden="true" />
            Salir
          </button>
        </div>
      </aside>
      <div className="workspace" inert={mobile && menu}>
        <header className="topbar">
          <button
            ref={menuButton}
            className="icon-button mobile-menu"
            aria-label="Abrir menú"
            aria-expanded={menu}
            onClick={() => setMenu(!menu)}
          >
            <Menu />
          </button>
          <div className="breadcrumb">
            Sala de pesas<span>/</span>
            <strong>
              {currentPage === "perfil"
                ? "Mi perfil"
                : nav.find((n) => n[0] === currentPage)?.[1]}
            </strong>
          </div>
          <div className="topbar-actions">
            <span className="portal-label">
              {role === "admin" ? "Administración" : "Portal estudiante"}
            </span>
            <button
              className="avatar small-avatar"
              aria-label={`${role === "admin" ? "RC" : "AM"} · Ver perfil`}
              onClick={() => go("perfil")}
            >
              {role === "admin" ? "RC" : "AM"}
            </button>
          </div>
        </header>
        <main id="main-content" className="main-content" tabIndex={-1}>
          <div className="page-heading">
            <div>
              <p className="eyebrow">
                {role === "admin"
                  ? "DEPORTES UCN / RECEPCIÓN"
                  : "DEPORTES UCN / SALA DE PESAS"}
              </p>
              <h1 ref={heading} tabIndex={-1}>
                {currentPage === "inicio"
                  ? role === "admin"
                    ? "Panel general"
                    : "Hola, Alex."
                  : {
                      horarios: "Encuentra tu horario.",
                      reservas: role === "admin" ? "Reservas" : "Mis reservas",
                      qr: "Tu acceso a la sala.",
                      acceso: "Control de acceso",
                      usuarios: "Usuarios",
                      perfil: "Mi perfil",
                    }[currentPage]}
              </h1>
              <p className="page-description">
                {currentPage === "inicio"
                  ? role === "admin"
                    ? "Reservas y asistencia por período."
                    : "Revisa tus reservas o elige un horario."
                  : {
                      horarios: "Elige un día y reserva un bloque disponible.",
                      reservas: "Consulta el estado de cada sesión.",
                      qr: "Presenta este código en recepción.",
                      acceso:
                        "Registra la entrada o salida con el QR de la reserva.",
                      usuarios: "Gestiona el acceso de la comunidad.",
                      perfil: "Información de tu cuenta.",
                    }[currentPage]}
              </p>
            </div>
            {currentPage === "inicio" && (
              <span className="today-label">
                <CalendarDays size={17} aria-hidden="true" />
                {new Intl.DateTimeFormat("es-CL", {
                  day: "numeric",
                  month: "long",
                  year: "numeric",
                }).format(new Date())}
              </span>
            )}
            {currentPage === "usuarios" && (
              <Button onClick={() => setShowMember(true)}>
                <Plus size={18} />
                Agregar usuario
              </Button>
            )}
          </div>

          {currentPage === "inicio" && role === "student" && (
            <StudentDashboard
              reservations={myReservations}
              upcoming={upcoming}
              onQr={() => go("qr")}
              onReserve={(date) => {
                if (date) setScheduleDate(date);
                go("horarios");
              }}
            />
          )}

          {currentPage === "inicio" && role === "admin" && (
            <AdminDashboard
              state={state}
              onAccess={() => go("acceso")}
              onReservations={() => go("reservas")}
              onUsers={() => go("usuarios")}
            />
          )}

          {currentPage === "horarios" && (
            <Schedule
              state={state}
              onSelect={setSelected}
              initialDate={scheduleDate}
            />
          )}
          {currentPage === "reservas" && (
            <Reservations
              reservations={
                role === "admin" ? state.reservations : myReservations
              }
              state={state}
              admin={role === "admin"}
              onCancel={setToCancel}
              onReserve={() => go("horarios")}
            />
          )}
          {currentPage === "qr" && (
            <QrPanel
              reservations={upcoming}
              member={member}
              onReserve={() => go("horarios")}
              onCopy={notify}
            />
          )}
          {currentPage === "acceso" && (
            <AccessPanel
              state={state}
              onSubmit={(code, action) =>
                update(
                  (s) => access(s, code, action),
                  action === "entry"
                    ? "Entrada registrada."
                    : "Salida registrada.",
                )
              }
            />
          )}
          {currentPage === "usuarios" && (
            <Members
              state={state}
              onToggle={(id) =>
                update(
                  (s) => ({
                    ...s,
                    members: s.members.map((m) =>
                      m.id === id ? { ...m, enabled: !m.enabled } : m,
                    ),
                  }),
                  "Estado actualizado.",
                )
              }
            />
          )}
          {currentPage === "perfil" && (
            <div className="profile-layout">
              <section className="card profile-card">
                <span className="avatar profile-avatar">
                  {role === "admin" ? "RC" : "AM"}
                </span>
                <h2>{role === "admin" ? "Recepción" : member.name}</h2>
                <Badge tone="blue">
                  {role === "admin" ? "Administrador" : member.category}
                </Badge>
                <dl>
                  <div>
                    <dt>Correo</dt>
                    <dd>
                      {role === "admin"
                        ? "recepcion@example.invalid"
                        : member.email}
                    </dd>
                  </div>
                  <div>
                    <dt>Estado</dt>
                    <dd>
                      {role === "admin" || member.enabled
                        ? "Habilitado"
                        : "Deshabilitado"}
                    </dd>
                  </div>
                  <div>
                    <dt>Recinto</dt>
                    <dd>Sala de pesas UCN</dd>
                  </div>
                </dl>
              </section>
            </div>
          )}
          <footer className="page-footer">
            <span>Deportes UCN</span>
            <span>Universidad Católica del Norte</span>
          </footer>
        </main>
      </div>
      {notice && (
        <div
          className={`toast ${failure ? "error" : ""}`}
          role={failure ? "alert" : "status"}
        >
          {failure ? <X size={19} /> : <CheckCircle2 size={19} />}
          <span>{notice}</span>
          <button
            className="icon-button"
            aria-label="Cerrar aviso"
            onClick={() => setNotice("")}
          >
            <X size={16} />
          </button>
        </div>
      )}
      {selected && (
        <Modal title="Confirmar reserva" onClose={() => setSelected(null)}>
          <div className="confirmation-detail">
            <SlotDetails slot={selected} />
            <p className="meta">
              <MapPin size={16} />
              Sala de pesas UCN
            </p>
          </div>
          <p className="muted">La reserva quedará a nombre de {member.name}.</p>
          <div className="modal-actions">
            <Button secondary onClick={() => setSelected(null)}>
              Volver
            </Button>
            <Button
              onClick={() => {
                if (
                  update(
                    (s) => reserve(s, DEMO_MEMBER, selected),
                    "Reserva confirmada. Tu QR ya está disponible.",
                  )
                ) {
                  setSelected(null);
                  go("reservas");
                }
              }}
            >
              <Check size={18} />
              Confirmar reserva
            </Button>
          </div>
        </Modal>
      )}
      {toCancel && (
        <Modal
          title="¿Cancelar esta reserva?"
          onClose={() => setToCancel(null)}
        >
          <div className="confirmation-detail">
            <SlotDetails slot={toCancel.slot} />
          </div>
          <p className="muted">El cupo quedará disponible para otra persona.</p>
          <div className="modal-actions">
            <Button secondary onClick={() => setToCancel(null)}>
              Mantener reserva
            </Button>
            <button
              className="button danger"
              onClick={() => {
                if (update((s) => cancel(s, toCancel.id), "Reserva cancelada."))
                  setToCancel(null);
              }}
            >
              Cancelar reserva
            </button>
          </div>
        </Modal>
      )}
      {showMember && (
        <MemberForm
          onClose={() => setShowMember(false)}
          onSave={(data) => {
            const email = data.email.trim().toLowerCase();
            if (state.members.some((m) => m.email.toLowerCase() === email))
              return "Este correo ya está registrado.";
            update(
              (s) => ({
                ...s,
                members: [
                  ...s.members,
                  { ...data, email, id: crypto.randomUUID(), enabled: true },
                ],
              }),
              "Usuario agregado.",
            );
            setShowMember(false);
            return "";
          }}
        />
      )}
    </div>
  );
}

function Schedule({
  state,
  onSelect,
  initialDate,
}: {
  state: DemoState;
  onSelect: (slot: Slot) => void;
  initialDate: string;
}) {
  const days = getDays();
  const [date, setDate] = useState(initialDate);
  return (
    <>
      <div className="schedule-toolbar">
        <div className="schedule-location">
          <MapPin size={18} />
          <strong>Sala de pesas</strong>
        </div>
        <span>Bloques de 90 minutos</span>
      </div>
      <div className="date-picker" aria-label="Seleccionar día">
        {days.map((day) => (
          <button
            key={day}
            aria-pressed={date === day}
            className={`date-option ${date === day ? "selected" : ""}`}
            onClick={() => setDate(day)}
          >
            <span>{shortDate(day, { weekday: "short" }).replace(".", "")}</span>
            <strong>{shortDate(day, { day: "2-digit" })}</strong>
            <span>{shortDate(day, { month: "short" }).replace(".", "")}</span>
          </button>
        ))}
      </div>
      <div className="section-heading compact">
        <h2 className="capitalize">{shortDate(date)}</h2>
        <span className="legend">
          <span />
          Con cupos
        </span>
      </div>
      <div className="slots-grid">
        {slotsFor(date).map((slot) => {
          const available = remaining(slot, state);
          const mine = state.reservations.some(
            (r) =>
              r.memberId === DEMO_MEMBER &&
              r.slot.id === slot.id &&
              r.status !== "Cancelada",
          );
          return (
            <article
              key={slot.id}
              className={`card slot-card ${!available ? "full" : ""}`}
            >
              <div className="slot-top">
                <span className="slot-icon">
                  <Dumbbell size={22} />
                </span>
                <Badge tone={mine ? "blue" : available ? "green" : "neutral"}>
                  {mine ? "Tu reserva" : available ? "Disponible" : "Completo"}
                </Badge>
              </div>
              <h3>
                {slot.start}
                <span>—</span>
                {slot.end}
              </h3>
              <p className="meta">
                <Clock3 size={15} />
                90 min
                <span className="separator" />
                <MapPin size={15} />
                Sala de pesas
              </p>
              <div className="capacity-info">
                <span>
                  {available
                    ? `${available} cupos disponibles`
                    : "Sin cupos disponibles"}
                </span>
                <span>
                  {slot.capacity - available}/{slot.capacity}
                </span>
              </div>
              <div className="capacity-bar">
                <span
                  style={{
                    width: `${((slot.capacity - available) / slot.capacity) * 100}%`,
                  }}
                />
              </div>
              <Button
                secondary={available > 0}
                disabled={
                  !available ||
                  mine ||
                  !state.members.find((m) => m.id === DEMO_MEMBER)?.enabled
                }
                onClick={() => onSelect(slot)}
              >
                {mine ? (
                  <>
                    <Check size={17} />
                    Ya reservado
                  </>
                ) : available ? (
                  <>
                    Reservar
                    <ArrowRight size={17} />
                  </>
                ) : (
                  "Bloque completo"
                )}
              </Button>
            </article>
          );
        })}
      </div>
    </>
  );
}

function ReservationList({
  reservations,
  state,
  admin,
  onCancel,
}: {
  reservations: Reservation[];
  state: DemoState;
  admin?: boolean;
  onCancel: (r: Reservation) => void;
}) {
  if (!reservations.length)
    return (
      <section className="card">
        <Empty title="Sin reservas todavía">
          Las reservas aparecerán aquí.
        </Empty>
      </section>
    );
  return (
    <div className="reservation-list">
      {reservations.map((r) => (
        <article className="card reservation-row" key={r.id}>
          <div className="reservation-date">
            <strong>{shortDate(r.slot.date, { day: "2-digit" })}</strong>
            <span>
              {shortDate(r.slot.date, { month: "short" }).replace(".", "")}
            </span>
          </div>
          <div className="reservation-info">
            <h3>
              {admin
                ? state.members.find((m) => m.id === r.memberId)?.name
                : "Sala de pesas"}
            </h3>
            <p>
              {shortDate(r.slot.date, { weekday: "long" })}
              <span>·</span>
              {r.slot.start} a {r.slot.end}
            </p>
            {r.entry && (
              <small>
                Entrada{" "}
                {new Date(r.entry).toLocaleTimeString("es-CL", {
                  hour: "2-digit",
                  minute: "2-digit",
                })}
                {r.exit &&
                  ` · Salida ${new Date(r.exit).toLocaleTimeString("es-CL", { hour: "2-digit", minute: "2-digit" })}`}
              </small>
            )}
          </div>
          <Badge tone={statusClass[r.status]}>{r.status}</Badge>
          {r.status === "Reservada" && (
            <button
              className="text-button cancel-link"
              onClick={() => onCancel(r)}
            >
              Cancelar
            </button>
          )}
        </article>
      ))}
    </div>
  );
}

function Reservations({
  reservations,
  state,
  admin,
  onCancel,
  onReserve,
}: {
  reservations: Reservation[];
  state: DemoState;
  admin: boolean;
  onCancel: (r: Reservation) => void;
  onReserve: () => void;
}) {
  const [filter, setFilter] = useState("Todas");
  const filters = ["Todas", "Reservada", "En sala", "Completada", "Cancelada"];
  const visible = [...reservations]
    .reverse()
    .filter((r) => filter === "Todas" || r.status === filter);
  return (
    <>
      <div className="filter-tabs" aria-label="Estado de reservas">
        {filters.map((f) => (
          <button
            key={f}
            aria-pressed={f === filter}
            className={f === filter ? "selected" : ""}
            onClick={() => setFilter(f)}
          >
            {f === "Reservada"
              ? "Pendientes"
              : f === "Completada"
                ? "Completadas"
                : f === "Cancelada"
                  ? "Canceladas"
                  : f}
            <span>
              {
                reservations.filter((r) => f === "Todas" || r.status === f)
                  .length
              }
            </span>
          </button>
        ))}
      </div>
      {visible.length ? (
        <ReservationList
          reservations={visible}
          state={state}
          admin={admin}
          onCancel={onCancel}
        />
      ) : (
        <section className="card">
          <Empty
            title="No hay reservas en esta vista"
            action={
              !admin && (
                <Button onClick={onReserve}>
                  Buscar un horario
                  <ArrowRight size={17} />
                </Button>
              )
            }
          >
            {reservations.length
              ? "Prueba con otro estado."
              : "Cuando reserves un bloque, lo encontrarás aquí."}
          </Empty>
        </section>
      )}
    </>
  );
}

function QrPanel({
  reservations,
  member,
  onReserve,
  onCopy,
}: {
  reservations: Reservation[];
  member: Member;
  onReserve: () => void;
  onCopy: (text: string, error?: boolean) => void;
}) {
  const [selectedId, setSelectedId] = useState("");
  const reservation =
    reservations.find((r) => r.id === selectedId) ?? reservations[0];
  if (!reservation)
    return (
      <section className="card">
        <Empty
          title="Primero, una reserva"
          action={
            <Button onClick={onReserve}>
              Reservar un horario
              <ArrowRight size={17} />
            </Button>
          }
        >
          Tu código estará disponible al confirmar un bloque.
        </Empty>
      </section>
    );
  return (
    <div className="qr-layout">
      <section className="card qr-card">
        <div className="qr-card-heading">
          <Brand />
        </div>
        <div className="qr-person">
          <h2>{member.name}</h2>
          <span>{member.category}</span>
        </div>
        {reservations.length > 1 && (
          <label className="field">
            Reserva
            <select
              value={reservation.id}
              onChange={(e) => setSelectedId(e.target.value)}
            >
              {reservations.map((r) => (
                <option value={r.id} key={r.id}>
                  {shortDate(r.slot.date)} · {r.slot.start}
                </option>
              ))}
            </select>
          </label>
        )}
        <div className="qr-frame">
          <QRCodeSVG
            value={ticket(reservation.id)}
            size={224}
            level="M"
            marginSize={3}
            title="Código QR de la reserva"
          />
        </div>
        <Badge tone={statusClass[reservation.status]}>
          {reservation.status}
        </Badge>
        <div className="qr-slot">
          <strong className="capitalize">
            {shortDate(reservation.slot.date)}
          </strong>
          <span>
            {reservation.slot.start} a {reservation.slot.end}
          </span>
          <span>Sala de pesas</span>
        </div>
        <button
          className="text-button"
          onClick={async () => {
            try {
              await navigator.clipboard.writeText(ticket(reservation.id));
              onCopy("Código copiado.");
            } catch {
              onCopy(
                "No pudimos copiar el código. Puedes seleccionarlo debajo.",
                true,
              );
            }
          }}
        >
          Copiar código
          <ArrowUpRight size={16} />
        </button>
        <code className="ticket-code">{ticket(reservation.id)}</code>
      </section>
      <aside className="qr-instructions">
        <span className="eyebrow">EN RECEPCIÓN</span>
        <h2>Cómo usar tu QR</h2>
        <ol>
          <li>
            <span>01</span>
            <div>
              <h3>Presenta tu código</h3>
              <p>Recepción registrará tu entrada.</p>
            </div>
          </li>
          <li>
            <span>02</span>
            <div>
              <h3>Disfruta tu sesión</h3>
              <p>Tu bloque dura 90 minutos.</p>
            </div>
          </li>
          <li>
            <span>03</span>
            <div>
              <h3>Registra tu salida</h3>
              <p>Muestra el mismo código al terminar.</p>
            </div>
          </li>
        </ol>
      </aside>
    </div>
  );
}

function AccessPanel({
  state,
  onSubmit,
}: {
  state: DemoState;
  onSubmit: (code: string, action: "entry" | "exit") => boolean;
}) {
  const [action, setAction] = useState<"entry" | "exit">("entry");
  const [code, setCode] = useState("");
  const [camera, setCamera] = useState(false);
  const [success, setSuccess] = useState(false);
  const candidates = state.reservations.filter(
    (r) => r.status === (action === "entry" ? "Reservada" : "En sala"),
  );
  function submit(value = code) {
    setSuccess(onSubmit(value, action));
  }
  return (
    <div className="access-layout">
      <section className="card scanner-card">
        <div className="access-tabs">
          <button
            aria-pressed={action === "entry"}
            className={action === "entry" ? "selected" : ""}
            onClick={() => {
              setAction("entry");
              setSuccess(false);
            }}
          >
            <ArrowDownLeft size={19} />
            Entrada
          </button>
          <button
            aria-pressed={action === "exit"}
            className={action === "exit" ? "selected" : ""}
            onClick={() => {
              setAction("exit");
              setSuccess(false);
            }}
          >
            <ArrowUpRight size={19} />
            Salida
          </button>
        </div>
        <div className={`scan-area ${success ? "scan-success" : ""}`}>
          {camera ? (
            <Suspense fallback={<p>Abriendo cámara…</p>}>
              <Camera
                onRead={(value) => {
                  setCamera(false);
                  setCode(value);
                  submit(value);
                }}
              />
            </Suspense>
          ) : (
            <>
              <span className="scan-corner top-left" />
              <span className="scan-corner top-right" />
              <span className="scan-corner bottom-left" />
              <span className="scan-corner bottom-right" />
              {success ? (
                <CheckCircle2 size={64} strokeWidth={1.3} />
              ) : (
                <ScanLine size={64} strokeWidth={1.3} />
              )}
              <h3>
                {success
                  ? `${action === "entry" ? "Entrada" : "Salida"} registrada`
                  : "Acerca el código QR"}
              </h3>
              <p>
                {success
                  ? "El historial ya está actualizado."
                  : "Activa la cámara para escanear."}
              </p>
            </>
          )}
        </div>
        <Button
          secondary
          onClick={() => {
            setSuccess(false);
            setCamera(!camera);
          }}
        >
          {camera ? "Cerrar cámara" : "Activar cámara"}
        </Button>
        <div className="or-divider">
          <span>o ingresa el código</span>
        </div>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            submit();
          }}
        >
          <label className="field">
            Código de la reserva
            <input
              value={code}
              onChange={(e) => {
                setCode(e.target.value);
                setSuccess(false);
              }}
              placeholder="UCN:…"
              required
            />
          </label>
          <button className="button" type="submit">
            Registrar {action === "entry" ? "entrada" : "salida"}
            <ArrowRight size={17} />
          </button>
        </form>
      </section>
      <aside>
        <section className="card access-summary">
          <div className="section-title">
            <h2>En sala</h2>
            <Badge tone="green">
              {state.reservations.filter((r) => r.status === "En sala").length}{" "}
              personas
            </Badge>
          </div>
          {state.reservations
            .filter((r) => r.status === "En sala")
            .map((r) => (
              <div className="present-member" key={r.id}>
                <span className="avatar">
                  {state.members
                    .find((m) => m.id === r.memberId)
                    ?.name.slice(0, 1)}
                </span>
                <div>
                  <strong>
                    {state.members.find((m) => m.id === r.memberId)?.name}
                  </strong>
                  <span>
                    Bloque {r.slot.start} a {r.slot.end}
                  </span>
                </div>
              </div>
            ))}
          {!state.reservations.some((r) => r.status === "En sala") && (
            <p className="muted">Sin entradas abiertas.</p>
          )}
        </section>
        <section className="card reservation-lookup">
          <span className="eyebrow">BÚSQUEDA MANUAL</span>
          <h3>
            {action === "entry" ? "Reservas pendientes" : "Visitas abiertas"}
          </h3>
          <p>Selecciona una persona para cargar su reserva.</p>
          {candidates.length ? (
            candidates.map((r) => (
              <button
                className="lookup-reservation"
                key={r.id}
                onClick={() => {
                  setCode(ticket(r.id));
                  setSuccess(false);
                }}
              >
                <span>
                  <strong>
                    {state.members.find((m) => m.id === r.memberId)?.name}
                  </strong>
                  <small>
                    {shortDate(r.slot.date, { day: "numeric", month: "short" })}{" "}
                    · {r.slot.start}
                  </small>
                </span>
                <ChevronRight size={18} />
              </button>
            ))
          ) : (
            <p className="muted">
              {action === "entry"
                ? "No hay reservas pendientes."
                : "No hay visitas abiertas."}
            </p>
          )}
        </section>
      </aside>
    </div>
  );
}

function Members({
  state,
  onToggle,
}: {
  state: DemoState;
  onToggle: (id: string) => void;
}) {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("Todos");
  const members = state.members.filter(
    (m) =>
      `${m.name} ${m.email} ${m.category}`
        .toLowerCase()
        .includes(search.toLowerCase()) &&
      (filter === "Todos" || m.enabled === (filter === "Habilitados")),
  );
  return (
    <section className="card members-card">
      <div className="members-toolbar">
        <label className="search">
          <Search size={18} aria-hidden="true" />
          <input
            aria-label="Buscar usuarios"
            placeholder="Buscar por nombre o correo"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </label>
        <select
          aria-label="Filtrar usuarios por estado"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        >
          {["Todos", "Habilitados", "Deshabilitados"].map((f) => (
            <option key={f}>{f}</option>
          ))}
        </select>
      </div>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Usuario</th>
              <th>Categoría</th>
              <th>Estado</th>
              <th>Acceso</th>
            </tr>
          </thead>
          <tbody>
            {members.map((m) => (
              <tr key={m.id}>
                <td>
                  <div className="table-person">
                    <span className="avatar">
                      {m.name
                        .split(" ")
                        .map((n) => n[0])
                        .slice(0, 2)
                        .join("")}
                    </span>
                    <span>
                      <strong>{m.name}</strong>
                      <small>{m.email}</small>
                    </span>
                  </div>
                </td>
                <td>{m.category}</td>
                <td>
                  <Badge tone={m.enabled ? "green" : "neutral"}>
                    {m.enabled ? "Habilitado" : "Deshabilitado"}
                  </Badge>
                </td>
                <td>
                  <button
                    className={`toggle ${m.enabled ? "enabled" : ""}`}
                    role="switch"
                    aria-checked={m.enabled}
                    aria-label={`Habilitar acceso de ${m.name}`}
                    onClick={() => onToggle(m.id)}
                  >
                    <span />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {!members.length && (
        <Empty title="Sin coincidencias">
          Prueba con otro nombre o filtro.
        </Empty>
      )}
      <div className="table-footer">
        {members.length} de {state.members.length} usuarios
      </div>
    </section>
  );
}

function MemberForm({
  onSave,
  onClose,
}: {
  onSave: (data: Omit<Member, "id" | "enabled">) => string;
  onClose: () => void;
}) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [category, setCategory] = useState("Estudiante");
  const [error, setError] = useState("");
  function submit(e: FormEvent) {
    e.preventDefault();
    if (!name.trim()) {
      setError("Ingresa un nombre.");
      return;
    }
    setError(onSave({ name: name.trim(), email, category }));
  }
  return (
    <Modal title="Agregar usuario" onClose={onClose}>
      <form onSubmit={submit} className="member-form">
        <label className="field">
          Nombre
          <input
            autoFocus
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            maxLength={100}
            autoComplete="off"
          />
        </label>
        <label className="field">
          Correo
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            placeholder="nombre@example.invalid"
            autoComplete="off"
            maxLength={200}
          />
        </label>
        <label className="field">
          Categoría
          <select
            value={category}
            onChange={(e) => setCategory(e.target.value)}
          >
            {[
              "Estudiante",
              "Docente",
              "Funcionario",
              "Deportista",
              "Externo",
            ].map((c) => (
              <option key={c}>{c}</option>
            ))}
          </select>
        </label>
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <div className="modal-actions">
          <Button secondary onClick={onClose}>
            Cancelar
          </Button>
          <button type="submit" className="button">
            Agregar usuario
          </button>
        </div>
      </form>
    </Modal>
  );
}

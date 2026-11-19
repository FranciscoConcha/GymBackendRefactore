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
  Users,
  X,
  type LucideIcon,
} from "lucide-react";
import { QRCodeSVG } from "qrcode.react";
import { useLocation, useNavigate } from "react-router-dom";
import AdminDashboard from "./pages/AdminDashboard";
import StudentDashboard from "./pages/StudentDashboard";
import { Badge, Button } from "./components/ui";
import {
  getDays,
  remaining,
  shortDate,
  type AppData,
  type Member,
  type Reservation,
  type Role,
  type Slot,
  durationMinutes,
  hasStarted,
} from "./domain";
import {
  api,
  ApiError,
  onUnauthorized,
  type UserResponse,
  type QrResponse,
  type RegisterRequest,
} from "./api";

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
  Reservado: "blue",
  Asistió: "neutral",
  Cancelado: "red",
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
  const [user, setUser] = useState<UserResponse | null>(null);
  const role: Role | null = user
    ? user.role === "Admin"
      ? "admin"
      : "student"
    : null;
  const [restoring, setRestoring] = useState(true);
  const [loginAdmin, setLoginAdmin] = useState(false);
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [revision, setRevision] = useState(0);
  const [qr, setQr] = useState<QrResponse | null>(null);
  const [state, setState] = useState<AppData>({
    members: [],
    reservations: [],
    slots: [],
  });
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
  const member: Member = {
    id: user?.id ?? 0,
    name: user?.name ?? "",
    email: user?.email ?? "",
    category: user?.userType ?? "",
    enabled: user?.enabled ?? false,
  };
  const nav = role === "admin" ? adminNav : studentNav;
  const currentPage =
    nav.some((n) => n[0] === page) || page === "perfil" ? page : "inicio";
  const myReservations = state.reservations.filter(
    (r) => r.memberId === user?.id,
  );
  const upcoming = [...myReservations]
    .filter((r) => r.status === "Reservado")
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
  useEffect(
    () =>
      onUnauthorized(() => {
        setUser(null);
        setState({ members: [], reservations: [], slots: [] });
        setQr(null);
        setSelected(null);
        setToCancel(null);
        setShowMember(false);
        setMenu(false);
        notify("Tu sesión ha expirado. Vuelve a iniciar sesión.", true);
        navigate("/", { replace: true });
      }),
    [navigate],
  );
  useEffect(() => {
    api
      .me()
      .then(setUser)
      .catch((error) => {
        if (!(error instanceof ApiError && error.status === 401))
          notify(error.message, true);
      })
      .finally(() => setRestoring(false));
  }, []);
  useEffect(() => {
    if (!user) return;
    const portal = user.role === "Admin" ? "admin" : "estudiante";
    if (segments[0] !== portal)
      navigate(`/${portal}/inicio`, { replace: true });
  }, [user, location.pathname, navigate]);
  useEffect(() => {
    if (!user) return;
    let active = true;
    Promise.all([
      api.reservations(user),
      user.role === "Admin" ? api.users() : Promise.resolve([user]),
      api.schedules(scheduleDate),
    ])
      .then(([reservations, users, slots]) => {
        if (active)
          setState({
            members: users.map((u) => ({ ...u, category: u.userType })),
            slots: slots.map((v) => ({
              id: v.id,
              date: v.date,
              start: v.startTime,
              end: v.endTime,
              capacity: v.capacity,
              occupied: v.reservedCount,
              zone: v.zone,
            })),
            reservations: reservations.map((r) => ({
              id: r.id,
              memberId: r.userId,
              userName: r.userName,
              status: r.status,
              slot: {
                id: r.scheduleSlotId,
                date: r.date,
                start: r.startTime,
                end: r.endTime,
                capacity: r.capacity,
                occupied: 0,
                zone: r.zone,
              },
            })),
          });
      })
      .catch((error) => {
        if (active) notify(error.message, true);
      });
    return () => {
      active = false;
    };
  }, [user, revision, scheduleDate, location.pathname]);
  useEffect(() => {
    setQr(null);
    if (!user || currentPage !== "qr") return;
    let active = true;
    api
      .qr()
      .then((value) => {
        if (active) setQr(value);
      })
      .catch((error) => {
        if (active) notify(error.message, true);
      });
    return () => {
      active = false;
    };
  }, [user, currentPage]);
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
  async function update(
    operation: () => Promise<unknown>,
    success: string,
  ): Promise<boolean> {
    if (busy) return false;
    setBusy(true);
    try {
      await operation();
      setRevision((v) => v + 1);
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
    } finally {
      setBusy(false);
    }
  }
  async function login(e: FormEvent) {
    e.preventDefault();
    if (busy) return;
    setBusy(true);
    try {
      const result = await api.login(loginAdmin, identifier, password);
      setUser(await api.me());
      setNotice(result.message);
      setIdentifier("");
    } catch (error) {
      notify(
        error instanceof Error ? error.message : "No se pudo iniciar sesión.",
        true,
      );
    } finally {
      setPassword("");
      setBusy(false);
    }
  }
  if (restoring)
    return (
      <main>
        <p role="status">Restaurando sesión…</p>
      </main>
    );
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
            <form onSubmit={login} className="member-form">
              <label className="field">
                Portal
                <select
                  value={loginAdmin ? "admin" : "user"}
                  onChange={(e) => {
                    setLoginAdmin(e.target.value === "admin");
                    setPassword("");
                  }}
                >
                  <option value="user">Usuario</option>
                  <option value="admin">Administración</option>
                </select>
              </label>
              <label className="field">
                {loginAdmin ? "Usuario" : "RUT"}
                <input
                  required
                  value={identifier}
                  onChange={(e) => setIdentifier(e.target.value)}
                  autoComplete="username"
                />
              </label>
              <label className="field">
                Contraseña
                <input
                  required
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="current-password"
                />
              </label>
              {notice && <p role="alert">{notice}</p>}
              <button className="button" disabled={busy} type="submit">
                Ingresar
              </button>
            </form>
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
            <span className="avatar">
              {member.name.slice(0, 2).toUpperCase()}
            </span>
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
            onClick={async () => {
              if (await update(() => api.logout(), "Sesión cerrada.")) {
                setUser(null);
                setState({ members: [], reservations: [], slots: [] });
                setQr(null);
                navigate("/");
              }
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
              aria-label={`${member.name.slice(0, 2).toUpperCase()} · Ver perfil`}
              onClick={() => go("perfil")}
            >
              {member.name.slice(0, 2).toUpperCase()}
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
                    : `Hola, ${member.name.split(" ")[0]}.`
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
                        "Registra la entrada o salida con el QR personal del día.",
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
              onDate={setScheduleDate}
              member={member}
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
          {currentPage === "qr" && <QrPanel qr={qr} onCopy={notify} />}
          {currentPage === "acceso" && (
            <AccessPanel
              onSubmit={(code, action) =>
                update(
                  () => api.access(code, action),
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
              onToggle={(id) => {
                const m = state.members.find((v) => v.id === id);
                if (m)
                  void update(
                    () => api.status(id, !m.enabled),
                    "Estado actualizado.",
                  );
              }}
            />
          )}

          {currentPage === "perfil" && (
            <div className="profile-layout">
              <section className="card profile-card">
                <span className="avatar profile-avatar">
                  {member.name.slice(0, 2).toUpperCase()}
                </span>
                <h2>{role === "admin" ? "Recepción" : member.name}</h2>
                <Badge tone="blue">
                  {role === "admin" ? "Administrador" : member.category}
                </Badge>
                <dl>
                  <div>
                    <dt>Correo</dt>
                    <dd>{member.email}</dd>
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
              onClick={async () => {
                if (
                  await update(
                    () => api.reserve(selected.id, selected.date),
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
              onClick={async () => {
                if (
                  await update(
                    () => api.cancel(toCancel.id),
                    "Reserva cancelada.",
                  )
                )
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
          onSave={async (data) => {
            if (await update(() => api.register(data), "Usuario agregado.")) {
              setShowMember(false);
              return "";
            }
            return "No se pudo agregar el usuario.";
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
  onDate,
  member,
}: {
  state: AppData;
  onSelect: (slot: Slot) => void;
  initialDate: string;
  onDate: (date: string) => void;
  member: Member;
}) {
  const days = getDays();
  const date = initialDate;
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
            onClick={() => onDate(day)}
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
        {state.slots
          .filter((s) => s.date === date)
          .map((slot) => {
            const available = remaining(slot);
            const started = hasStarted(slot);
            const mine = state.reservations.some(
              (r) =>
                r.memberId === member.id &&
                r.slot.id === slot.id &&
                r.slot.date === slot.date &&
                r.status !== "Cancelado",
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
                    {mine
                      ? "Tu reserva"
                      : available
                        ? "Disponible"
                        : "Completo"}
                  </Badge>
                </div>
                <h3>
                  {slot.start}
                  <span>—</span>
                  {slot.end}
                </h3>
                <p className="meta">
                  <Clock3 size={15} />
                  {durationMinutes(slot)} min
                  <span className="separator" />
                  <MapPin size={15} />
                  {slot.zone}
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
                      width: `${slot.capacity > 0 ? ((slot.capacity - available) / slot.capacity) * 100 : 0}%`,
                    }}
                  />
                </div>
                <Button
                  secondary={available > 0}
                  disabled={!available || mine || started || !member.enabled}
                  onClick={() => onSelect(slot)}
                >
                  {mine ? (
                    <>
                      <Check size={17} />
                      Ya reservado
                    </>
                  ) : started ? (
                    "Bloque iniciado"
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
  state: AppData;
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
          </div>
          <Badge tone={statusClass[r.status]}>{r.status}</Badge>
          {r.status === "Reservado" && (
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
  state: AppData;
  admin: boolean;
  onCancel: (r: Reservation) => void;
  onReserve: () => void;
}) {
  const [filter, setFilter] = useState("Todas");
  const filters = ["Todas", "Reservado", "Asistió", "Cancelado"];
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
            {f === "Reservado"
              ? "Pendientes"
              : f === "Asistió"
                ? "Asistencias"
                : f === "Cancelado"
                  ? "Cancelados"
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
  qr,
  onCopy,
}: {
  qr: QrResponse | null;
  onCopy: (text: string, error?: boolean) => void;
}) {
  if (!qr)
    return (
      <section className="card">
        <p role="status">Código no disponible.</p>
      </section>
    );
  return (
    <section className="card qr-card">
      <Brand />
      <h2>{qr.name}</h2>
      <p>QR personal del día {shortDate(qr.date)}</p>
      <div className="qr-frame">
        <QRCodeSVG
          value={qr.token}
          size={224}
          marginSize={3}
          title="Código QR personal del día"
        />
      </div>
      <p>
        Válido desde {new Date(qr.validFrom).toLocaleString("es-CL")} hasta{" "}
        {new Date(qr.validUntil).toLocaleString("es-CL")}
      </p>
      <button
        className="text-button"
        onClick={async () => {
          try {
            await navigator.clipboard.writeText(qr.token);
            onCopy("Código copiado.");
          } catch {
            onCopy("No pudimos copiar el código.", true);
          }
        }}
      >
        Copiar código
      </button>
      <code className="ticket-code">{qr.token}</code>
    </section>
  );
}

function AccessPanel({
  onSubmit,
}: {
  onSubmit: (code: string, action: "entry" | "exit") => Promise<boolean>;
}) {
  const [action, setAction] = useState<"entry" | "exit">("entry");
  const [code, setCode] = useState("");
  const [camera, setCamera] = useState(false);
  const [success, setSuccess] = useState(false);
  async function submit(value = code) {
    setSuccess(await onSubmit(value, action));
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
                  ? "La API confirmó el registro."
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
            Código personal del día
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
      <aside className="card">
        <h2>Asistencias</h2>
        <p>El historial de entradas y salidas no está disponible en la API.</p>
      </aside>
    </div>
  );
}

function Members({
  state,
  onToggle,
}: {
  state: AppData;
  onToggle: (id: number) => void;
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
  onSave: (data: RegisterRequest) => Promise<string>;
  onClose: () => void;
}) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [category, setCategory] = useState("Estudiante");
  const [rut, setRut] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [error, setError] = useState("");
  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!name.trim()) {
      setError("Ingresa un nombre.");
      return;
    }
    setError(
      await onSave({
        name: name.trim(),
        email,
        rut,
        password: newPassword,
        userType: category as RegisterRequest["userType"],
      }),
    );
  }
  return (
    <Modal title="Agregar usuario" onClose={onClose}>
      <form onSubmit={submit} className="member-form">
        <label className="field">
          RUT
          <input
            required
            value={rut}
            onChange={(e) => setRut(e.target.value)}
          />
        </label>
        <label className="field">
          Contraseña inicial
          <input
            type="password"
            required
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            autoComplete="new-password"
          />
        </label>
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
            {["Estudiante", "Administrador"].map((c) => (
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

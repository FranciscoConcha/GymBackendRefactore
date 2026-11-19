import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from "react";
import {
  ArrowRight,
  ArrowUpRight,
  CalendarDays,
  Check,
  CheckCircle2,
  ChevronRight,
  Clock3,
  Dumbbell,
  FileSpreadsheet,
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
import ReceptionDashboard from "./pages/ReceptionDashboard";
import GroupBooking from "./pages/GroupBooking";
import ExcelPanel from "./pages/ExcelPanel";
import AccountPage from "./pages/AccountPage";
import AccessPanel from "./pages/AccessPanel";
import { Badge, Button } from "./components/ui";
import {
  access,
  cancel,
  DEMO_MEMBER,
  CATEGORIES,
  categoryPriority,
  formatRut,
  isValidRut,
  normalizeRut,
  reconcileAttendance,
  reserveGroup,
  getDays,
  loadDemo,
  remaining,
  reserve,
  validateReservation,
  shortDate,
  slotsFor,
  slotEnd,
  STORAGE_KEY,
  ticket,
  type DemoState,
  type Member,
  type Reservation,
  type Role,
  type Slot,
} from "./domain";

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
  ["grupos", "Sesiones por rama", Users],
  ["archivos", "Excel", FileSpreadsheet],
];
const receptionNav: [string, string, LucideIcon][] = [
  ["inicio", "Estado actual", Users],
  ["acceso", "Escanear QR", ScanLine],
];
const rolePath = (role: Role | null) =>
  role === "admin"
    ? "admin"
    : role === "reception"
      ? "recepcion"
      : "estudiante";
const statusClass: Record<string, string> = {
  Reservada: "blue",
  "En sala": "green",
  "Fuera de sala": "blue",
  Completada: "neutral",
  Cancelada: "red",
  "No asistió": "gold",
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
        : segments[0] === "recepcion"
          ? "reception"
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
  const [editingMember, setEditingMember] = useState<Member | null>(null);
  const [loginMemberId, setLoginMemberId] = useState(
    () =>
      state.members.find((value) => value.category === "Deportista")?.id ??
      DEMO_MEMBER,
  );
  const [scheduleDate, setScheduleDate] = useState(() => getDays()[0]);
  const heading = useRef<HTMLHeadingElement>(null);
  const requestedMemberId =
    new URLSearchParams(location.search).get("usuario") ?? DEMO_MEMBER;
  const member =
    state.members.find((m) => m.id === requestedMemberId) ??
    state.members.find((m) => m.id === DEMO_MEMBER)!;
  const nav =
    role === "admin"
      ? adminNav
      : role === "reception"
        ? receptionNav
        : studentNav;
  const roleName =
    role === "admin"
      ? "Administrador"
      : role === "reception"
        ? "Recepcionista"
        : member.name;
  const initials =
    role === "admin"
      ? "AD"
      : role === "reception"
        ? "RC"
        : member.name
            .split(" ")
            .map((value) => value[0])
            .slice(0, 2)
            .join("");
  const currentPage =
    nav.some((n) => n[0] === page) ||
    (page === "perfil" && role !== "reception")
      ? page
      : "inicio";
  const myReservations = state.reservations.filter(
    (r) => r.memberId === member.id,
  );
  const upcoming = [...myReservations]
    .filter((r) => ["Reservada", "En sala", "Fuera de sala"].includes(r.status))
    .sort((a, b) =>
      `${a.slot.date}${a.slot.start}`.localeCompare(
        `${b.slot.date}${b.slot.start}`,
      ),
    );

  useEffect(() => {
    const refresh = () => setState((current) => reconcileAttendance(current));
    refresh();
    const timer = setInterval(refresh, 30000);
    return () => clearInterval(timer);
  }, []);
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
    navigate(
      `/${rolePath(role)}/${destination}${role === "student" && member.id !== DEMO_MEMBER ? `?usuario=${encodeURIComponent(member.id)}` : ""}`,
    );
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
      setState(operation(reconcileAttendance(state)));
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
    navigate(
      `/${rolePath(nextRole)}/inicio${nextRole === "student" && loginMemberId !== DEMO_MEMBER ? `?usuario=${encodeURIComponent(loginMemberId)}` : ""}`,
    );
  }

  if (
    [
      "recuperar-contrasena",
      "activar-cuenta",
      "restablecer-contrasena",
    ].includes(segments[0])
  )
    return (
      <AccountPage
        mode={
          segments[0] === "recuperar-contrasena"
            ? "recover"
            : segments[0] === "activar-cuenta"
              ? "activate"
              : "reset"
        }
        onBack={() => navigate("/")}
      />
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
            <p className="intro">Selecciona tu portal.</p>
            <label className="field login-member">
              Perfil de usuario
              <select
                value={loginMemberId}
                onChange={(event) => setLoginMemberId(event.target.value)}
              >
                {[...state.members]
                  .sort(
                    (a, b) =>
                      categoryPriority(a.category) -
                      categoryPriority(b.category),
                  )
                  .map((value) => (
                    <option value={value.id} key={value.id}>
                      {value.name} · {value.branch ?? value.category}
                    </option>
                  ))}
              </select>
            </label>
            <button className="profile-option" onClick={() => enter("student")}>
              <span className="option-icon">
                <UserRound />
              </span>
              <span>
                <strong>Usuario / Estudiante</strong>
                <small>Reservas y código de acceso</small>
              </span>
              <ArrowRight size={20} />
            </button>
            <button
              className="profile-option"
              onClick={() => enter("reception")}
            >
              <span className="option-icon">
                <ScanLine />
              </span>
              <span>
                <strong>Recepcionista</strong>
                <small>Escaneo y estado actual</small>
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
            <button
              className="text-button"
              onClick={() => navigate("/recuperar-contrasena")}
            >
              Olvidé mi contraseña
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
          {role === "admin"
            ? "ADMINISTRACIÓN"
            : role === "reception"
              ? "RECEPCIÓN"
              : "MI ESPACIO"}
        </div>
        <nav aria-label="Navegación principal">
          {nav.map(([id, label, Icon]) => (
            <a
              key={id}
              href={`/${rolePath(role)}/${id}${role === "student" && member.id !== DEMO_MEMBER ? `?usuario=${encodeURIComponent(member.id)}` : ""}`}
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
          <button
            className="sidebar-profile"
            onClick={() => go(role === "reception" ? "inicio" : "perfil")}
          >
            <span className="avatar">{initials}</span>
            <span>
              <strong>{roleName}</strong>
              <small>
                {role === "student"
                  ? (member.branch ?? member.category)
                  : "Deportes UCN"}
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
              {role === "admin"
                ? "Administración"
                : role === "reception"
                  ? "Recepción"
                  : "Portal usuario"}
            </span>
            <button
              className="avatar small-avatar"
              aria-label={`${initials} · ${role === "reception" ? "Ver estado actual" : "Ver perfil"}`}
              onClick={() => go(role === "reception" ? "inicio" : "perfil")}
            >
              {initials}
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
                    : role === "reception"
                      ? "Estado actual de la sala"
                      : `Hola, ${member.name.split(" ")[0]}.`
                  : {
                      horarios: "Encuentra tu horario.",
                      reservas: role === "admin" ? "Reservas" : "Mis reservas",
                      qr: "Tu acceso a la sala.",
                      acceso: "Control de acceso",
                      usuarios: "Usuarios",
                      grupos: "Sesiones por rama",
                      archivos: "Importar y exportar",
                      perfil: "Mi perfil",
                    }[currentPage]}
              </h1>
              <p className="page-description">
                {currentPage === "inicio"
                  ? role === "admin"
                    ? "Reservas y asistencia por período."
                    : role === "reception"
                      ? "Escaneo de QR e identificación para el acceso."
                      : "Revisa tus reservas o elige un horario."
                  : {
                      horarios: "Elige un día y reserva un bloque disponible.",
                      reservas: "Consulta el estado de cada sesión.",
                      qr: "Presenta este código en recepción.",
                      acceso:
                        "Registra la entrada o salida con el QR de la reserva.",
                      usuarios: "Gestiona el acceso de la comunidad.",
                      grupos:
                        "Reserva cupos para deportistas de una misma rama.",
                      archivos:
                        "Archivos Excel de usuarios y registros operativos.",
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
          {currentPage === "inicio" && role === "reception" && (
            <ReceptionDashboard state={state} onScan={() => go("acceso")} />
          )}
          {currentPage === "grupos" && role === "admin" && (
            <GroupBooking
              state={state}
              onReserve={(branch, ids, slot) =>
                update(
                  (current) => reserveGroup(current, role, branch, ids, slot),
                  "Sesión de rama confirmada.",
                )
              }
            />
          )}
          {currentPage === "archivos" && role === "admin" && (
            <ExcelPanel
              state={state}
              onImport={(operation) =>
                update(
                  operation,
                  "Importación confirmada. Las cuentas están pendientes de activación.",
                )
              }
            />
          )}

          {currentPage === "horarios" && (
            <Schedule
              state={state}
              onSelect={setSelected}
              initialDate={scheduleDate}
              memberId={member.id}
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
              role={role}
              onSubmit={(code, action, document) =>
                update(
                  (s) => access(s, code, action, new Date(), role, document),
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
              onEdit={setEditingMember}
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
                <span className="avatar profile-avatar">{initials}</span>
                <h2>{roleName}</h2>
                <Badge tone="blue">
                  {role === "admin" ? "Administrador" : member.category}
                </Badge>
                <dl>
                  {role === "student" && (
                    <>
                      <div>
                        <dt>RUT</dt>
                        <dd>{formatRut(member.rut) || "Pendiente"}</dd>
                      </div>
                      {member.branch && (
                        <div>
                          <dt>Rama deportiva</dt>
                          <dd>{member.branch}</dd>
                        </div>
                      )}
                    </>
                  )}
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
                    (s) => reserve(s, member.id, selected),
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
      {role === "admin" && (showMember || editingMember) && (
        <MemberForm
          initial={editingMember ?? undefined}
          onClose={() => {
            setShowMember(false);
            setEditingMember(null);
          }}
          onSave={(data) => {
            const email = data.email.trim().toLowerCase();
            if (
              state.members.some(
                (m) =>
                  m.id !== editingMember?.id && m.email.toLowerCase() === email,
              )
            )
              return "Este correo ya está registrado.";
            if (
              state.members.some(
                (m) =>
                  m.id !== editingMember?.id &&
                  normalizeRut(m.rut) === normalizeRut(data.rut),
              )
            )
              return "Este RUT ya está registrado.";
            update(
              (s) => ({
                ...s,
                members: editingMember
                  ? s.members.map((value) =>
                      value.id === editingMember.id
                        ? { ...value, ...data, email }
                        : value,
                    )
                  : [
                      ...s.members,
                      {
                        ...data,
                        email,
                        id: crypto.randomUUID(),
                        enabled: true,
                        accountStatus: "Pendiente de activación",
                      },
                    ],
              }),
              editingMember
                ? "Usuario actualizado."
                : "Usuario agregado. Activación pendiente.",
            );
            setShowMember(false);
            setEditingMember(null);
            return "";
          }}
        />
      )}
      {role === "student" &&
        !selected &&
        !toCancel &&
        (state.alerts ?? []).some(
          (alert) => alert.memberId === member.id && !alert.readAt,
        ) && (
          <Modal
            title="Tienes reservas sin asistencia"
            onClose={() =>
              setState((current) => ({
                ...current,
                alerts: (current.alerts ?? []).map((alert) =>
                  alert.memberId === member.id && !alert.readAt
                    ? { ...alert, readAt: new Date().toISOString() }
                    : alert,
                ),
              }))
            }
          >
            <p className="muted">No registramos tu ingreso a estas sesiones:</p>
            <ul className="attendance-alerts">
              {(state.alerts ?? [])
                .filter(
                  (alert) => alert.memberId === member.id && !alert.readAt,
                )
                .map((alert) => {
                  const reservation = state.reservations.find(
                    (value) => value.id === alert.reservationId,
                  );
                  return (
                    <li key={alert.id}>
                      {reservation
                        ? `${shortDate(reservation.slot.date)} · ${reservation.slot.start} a ${reservation.slot.end}`
                        : "Sesión finalizada sin ingreso registrado"}
                    </li>
                  );
                })}
            </ul>
            <p className="muted">
              Si necesitas corregir una asistencia, contacta a administración.
            </p>
            <div className="modal-actions">
              <Button
                onClick={() =>
                  setState((current) => ({
                    ...current,
                    alerts: (current.alerts ?? []).map((alert) =>
                      alert.memberId === member.id && !alert.readAt
                        ? { ...alert, readAt: new Date().toISOString() }
                        : alert,
                    ),
                  }))
                }
              >
                Entendido
              </Button>
            </div>
          </Modal>
        )}
    </div>
  );
}

function Schedule({
  state,
  onSelect,
  initialDate,
  memberId,
}: {
  state: DemoState;
  onSelect: (slot: Slot) => void;
  initialDate: string;
  memberId: string;
}) {
  const days = getDays();
  const [date, setDate] = useState(initialDate);
  const profile = state.members.find((value) => value.id === memberId);
  return (
    <>
      <div className="schedule-profile">
        <Badge tone="blue">
          {profile?.category}
          {profile?.branch ? ` · ${profile.branch}` : ""}
        </Badge>
        {profile?.category === "Deportista" ? (
          <span>Puedes reservar más de un bloque por día.</span>
        ) : profile?.category === "Estudiante" ? (
          <span>Máximo una reserva por día.</span>
        ) : null}
      </div>
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
              r.memberId === memberId &&
              r.slot.id === slot.id &&
              r.status !== "Cancelada",
          );
          const ended = slotEnd(slot) <= new Date();
          let unavailable = "";
          try {
            validateReservation(state, memberId, slot);
          } catch (cause) {
            unavailable =
              cause instanceof Error ? cause.message : "No disponible";
          }
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
                disabled={!available || mine || !!unavailable}
                onClick={() => onSelect(slot)}
              >
                {mine ? (
                  <>
                    <Check size={17} />
                    Ya reservado
                  </>
                ) : ended ? (
                  "Sesión finalizada"
                ) : available && unavailable ? (
                  "No disponible"
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
            {r.createdAt && (
              <small>
                Reserva realizada:{" "}
                {new Date(r.createdAt).toLocaleString("es-CL", {
                  dateStyle: "short",
                  timeStyle: "short",
                })}
              </small>
            )}
            {(r.branch || r.groupId) && (
              <small>
                {r.branch}
                {r.groupId ? " · Sesión de rama" : ""}
              </small>
            )}
            {!!r.visits?.length && (
              <details className="visit-details">
                <summary>
                  {r.visits.length}{" "}
                  {r.visits.length === 1
                    ? "visita registrada"
                    : "visitas registradas"}
                </summary>
                <ol>
                  {r.visits.map((visit) => (
                    <li key={visit.id}>
                      Entrada{" "}
                      {new Date(visit.entryAt).toLocaleTimeString("es-CL", {
                        hour: "2-digit",
                        minute: "2-digit",
                      })}{" "}
                      ·{" "}
                      {visit.exitAt
                        ? `Salida ${new Date(visit.exitAt).toLocaleTimeString("es-CL", { hour: "2-digit", minute: "2-digit" })}`
                        : "Dentro de la sala"}
                    </li>
                  ))}
                </ol>
              </details>
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
  const [branch, setBranch] = useState("");
  const branches = [
    ...new Set(
      state.members.flatMap((member) => (member.branch ? [member.branch] : [])),
    ),
  ].sort();
  const scoped = reservations.filter(
    (reservation) =>
      !branch ||
      (reservation.branch ??
        state.members.find((member) => member.id === reservation.memberId)
          ?.branch) === branch,
  );
  const filters = [
    "Todas",
    "Reservada",
    "En sala",
    "Fuera de sala",
    "Completada",
    "Cancelada",
    "No asistió",
  ];
  const visible = [...scoped]
    .reverse()
    .filter((r) => filter === "Todas" || r.status === filter);
  return (
    <>
      {admin && (
        <label className="field branch-filter">
          Rama deportiva
          <select
            value={branch}
            onChange={(event) => setBranch(event.target.value)}
          >
            <option value="">Todas las ramas</option>
            {branches.map((value) => (
              <option key={value}>{value}</option>
            ))}
          </select>
        </label>
      )}
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
              {scoped.filter((r) => f === "Todas" || r.status === f).length}
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
          <strong className="qr-rut">
            {formatRut(member.rut) || "RUT pendiente"}
          </strong>
          <span>{member.category}</span>
          {member.branch && <span>{member.branch}</span>}
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
              <p>
                Preséntalo junto a tu Carnet, TNE o credencial universitaria.
              </p>
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
              <p>
                Usa el mismo QR para salir y volver a ingresar mientras siga
                vigente tu sesión.
              </p>
            </div>
          </li>
        </ol>
      </aside>
    </div>
  );
}

function Members({
  state,
  onToggle,
  onEdit,
}: {
  state: DemoState;
  onToggle: (id: string) => void;
  onEdit: (member: Member) => void;
}) {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("Todos");
  const [branch, setBranch] = useState("");
  const [category, setCategory] = useState("");
  const branches = [
    ...new Set(
      state.members.flatMap((member) => (member.branch ? [member.branch] : [])),
    ),
  ].sort();
  const members = state.members
    .filter(
      (m) =>
        `${m.name} ${m.rut} ${normalizeRut(m.rut)} ${m.email} ${m.category} ${m.branch ?? ""}`
          .toLowerCase()
          .includes(search.toLowerCase()) &&
        (filter === "Todos" || m.enabled === (filter === "Habilitados")) &&
        (!branch || m.branch === branch) &&
        (!category || m.category === category),
    )
    .sort(
      (a, b) =>
        categoryPriority(a.category) - categoryPriority(b.category) ||
        a.name.localeCompare(b.name),
    );
  return (
    <section className="card members-card">
      <div className="members-toolbar">
        <label className="search">
          <Search size={18} aria-hidden="true" />
          <input
            aria-label="Buscar usuarios"
            placeholder="Nombre, RUT o correo"
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
        <select
          aria-label="Filtrar por categoría"
          value={category}
          onChange={(event) => setCategory(event.target.value)}
        >
          <option value="">Todas las categorías</option>
          {CATEGORIES.map((value) => (
            <option key={value}>{value}</option>
          ))}
        </select>
        <select
          aria-label="Filtrar por rama deportiva"
          value={branch}
          onChange={(event) => setBranch(event.target.value)}
        >
          <option value="">Todas las ramas</option>
          {branches.map((value) => (
            <option key={value}>{value}</option>
          ))}
        </select>
      </div>
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              <th>Usuario</th>
              <th>Categoría</th>
              <th>Rama</th>
              <th>Estado</th>
              <th>Acciones</th>
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
                      <small>{formatRut(m.rut) || "RUT pendiente"}</small>
                    </span>
                  </div>
                </td>
                <td>{m.category}</td>
                <td>{m.branch ?? "—"}</td>
                <td>
                  <Badge tone={m.enabled ? "green" : "neutral"}>
                    {m.enabled ? "Habilitado" : "Deshabilitado"}
                  </Badge>
                  {m.accountStatus === "Pendiente de activación" && (
                    <small className="account-pending">
                      Activación pendiente
                    </small>
                  )}
                </td>
                <td>
                  <button
                    className="text-button"
                    aria-label={`Editar ${m.name}`}
                    onClick={() => onEdit(m)}
                  >
                    Editar
                  </button>
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
  initial,
}: {
  onSave: (data: Omit<Member, "id" | "enabled">) => string;
  onClose: () => void;
  initial?: Member;
}) {
  const [name, setName] = useState(initial?.name ?? "");
  const [rut, setRut] = useState(initial?.rut ?? "");
  const [email, setEmail] = useState(initial?.email ?? "");
  const [category, setCategory] = useState(initial?.category ?? "Deportista");
  const [branch, setBranch] = useState(initial?.branch ?? "");
  const [error, setError] = useState("");
  function submit(e: FormEvent) {
    e.preventDefault();
    if (name.trim().split(/\s+/).length < 2) {
      setError("Ingresa nombre y apellido.");
      return;
    }
    if (!isValidRut(rut)) {
      setError("Ingresa un RUT válido con su dígito verificador.");
      return;
    }
    if (category === "Deportista" && !branch.trim()) {
      setError("Indica la rama deportiva.");
      return;
    }
    setError(
      onSave({
        name: name.trim(),
        rut: formatRut(rut),
        email,
        category,
        branch: category === "Deportista" ? branch.trim() : undefined,
      }),
    );
  }
  return (
    <Modal
      title={initial ? "Editar usuario" : "Agregar usuario"}
      onClose={onClose}
    >
      <form onSubmit={submit} className="member-form">
        <label className="field">
          Nombre y apellido
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
          RUT
          <input
            value={rut}
            onChange={(event) => setRut(event.target.value)}
            required
            maxLength={12}
            placeholder="12.345.678-5"
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
            {CATEGORIES.map((c) => (
              <option key={c}>{c}</option>
            ))}
          </select>
        </label>
        {category === "Deportista" && (
          <label className="field">
            Rama deportiva
            <input
              value={branch}
              onChange={(event) => setBranch(event.target.value)}
              required
              maxLength={80}
              placeholder="Ej. Fútbol"
            />
          </label>
        )}
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
            {initial ? "Guardar cambios" : "Agregar usuario"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

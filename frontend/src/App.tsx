import { SelectField } from "./components/SelectField";
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
  Users,
  X,
  type LucideIcon,
} from "lucide-react";
import { useLocation, useNavigate } from "react-router-dom";
import AdminDashboard from "./pages/AdminDashboard";
import StudentDashboard from "./pages/StudentDashboard";
import ReceptionDashboard from "./pages/ReceptionDashboard";
import GroupBooking from "./pages/GroupBooking";
import ExcelPanel from "./pages/ExcelPanel";
import AccessPanel from "./pages/AccessPanel";
import { Badge, Button } from "./components/ui";
import {
  CATEGORIES,
  formatRut,
  isValidRut,
  getDays,
  remaining,
  shortDate,
  hasStarted,
  slotFrom,
  memberFrom,
  reservationFrom,
  emptyState,
  type ViewState,
  type Member,
  type Reservation,
  type Role,
  type Slot,
} from "./domain";
import { api, type UserResponse } from "./api";
import Session from "./Session";
import { LoadStatus, useApiData } from "./remote";
import BlockQr from "./pages/BlockQr";
import { BranchesPanel, HistoryPanel, AlertsPanel } from "./pages/Records";

export default function App() {
  return <Session>{(user) => <Workspace key={user.id} user={user} />}</Session>;
}

const studentNav: [string, string, LucideIcon][] = [
  ["inicio", "Inicio", LayoutDashboard],
  ["horarios", "Reservar", CalendarDays],
  ["reservas", "Mis reservas", History],
  ["qr", "Mi código QR", QrCode],
  ["visitas", "Mis visitas", History],
  ["grupos", "Grupos de mi rama", Users],
];
const adminNav: [string, string, LucideIcon][] = [
  ["inicio", "Resumen", LayoutDashboard],
  ["acceso", "Control de acceso", ScanLine],
  ["usuarios", "Usuarios", Users],
  ["reservas", "Reservas", CalendarDays],
  ["grupos", "Sesiones por rama", Users],
  ["archivos", "Excel", FileSpreadsheet],
  ["ramas", "Ramas", Users],
  ["alertas", "Inasistencias", History],
  ["visitas", "Visitas", History],
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
  Reservado: "blue",
  Asistió: "green",
  Cancelado: "red",
  Inasistencia: "gold",
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
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
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

function Workspace({ user }: { user: UserResponse }) {
  const location = useLocation();
  const navigate = useNavigate();
  const segments = location.pathname.split("/").filter(Boolean);
  const role: Role =
    user.role === "Admin"
      ? "admin"
      : user.role === "Recepcionista"
        ? "reception"
        : "student";
  const [state, setState] = useState<ViewState>(emptyState);
  const [revision, setRevision] = useState(0);
  const refresh = () => setRevision((value) => value + 1);
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const reserveKey = useRef<{ body: string; key: string } | null>(null);
  const remote = useApiData(async () => {
    const [branches, members, reservations, alerts] = await Promise.all([
      role === "admin" ? api.branches() : Promise.resolve([]),
      role === "admin" ? api.users() : Promise.resolve([user]),
      role === "reception" ? Promise.resolve([]) : api.reservations(user),
      role === "student" ? api.alerts() : Promise.resolve([]),
    ]);
    return {
      members: members.map(memberFrom),
      reservations: reservations.map((r) => reservationFrom(r, branches)),
      alerts,
      branches,
    };
  }, [user, revision, location.pathname]);
  useEffect(() => {
    if (remote.data) setState(remote.data);
  }, [remote.data]);
  useEffect(() => {
    if (segments[0] !== rolePath(role))
      navigate(`/${rolePath(role)}/inicio`, { replace: true });
  }, [role, location.pathname]);
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
  const [scheduleDate, setScheduleDate] = useState(() => getDays()[0]);
  const heading = useRef<HTMLHeadingElement>(null);
  const member = memberFrom(user);
  const nav =
    role === "admin"
      ? adminNav
      : role === "reception"
        ? receptionNav
        : studentNav.filter(
            ([id]) => id !== "grupos" || user.category === "Deportista",
          );
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
    .filter((r) => ["Reservado", "Asistió"].includes(r.status))
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
    if (!notice) return;
    const timeout = setTimeout(() => setNotice(""), 6500);
    return () => clearTimeout(timeout);
  }, [notice]);

  function go(destination: string) {
    navigate(`/${rolePath(role)}/${destination}`);
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
    if (submitting.current) return false;
    submitting.current = true;
    setBusy(true);
    try {
      await operation();
      refresh();
      notify(success);
      return true;
    } catch (cause) {
      notify(
        cause instanceof Error
          ? cause.message
          : "No se pudo completar la acción.",
        true,
      );
      return false;
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }
  async function acknowledge() {
    await update(
      () =>
        Promise.all(
          state.alerts
            .filter((a) => !a.acknowledgedAt)
            .map((a) => api.acknowledge(a.id)),
        ),
      "Alertas leídas.",
    );
  }
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
              href={`/${rolePath(role)}/${id}`}
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
            onClick={() =>
              void update(async () => {
                await api.logout();
                window.location.assign("/");
              }, "Sesión cerrada.")
            }
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
                      ramas: "Ramas deportivas",
                      alertas: "Inasistencias",
                      visitas:
                        role === "admin"
                          ? "Historial de visitas"
                          : "Historial de mis visitas",
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
              onAccess={() => go("acceso")}
              onReservations={() => go("reservas")}
              onUsers={() => go("usuarios")}
            />
          )}
          {currentPage === "inicio" && role === "reception" && (
            <ReceptionDashboard onScan={() => go("acceso")} />
          )}
          <LoadStatus error={remote.error} loading={remote.loading} />
          {currentPage === "grupos" && (
            <GroupBooking admin={role === "admin"} />
          )}
          {currentPage === "archivos" && role === "admin" && <ExcelPanel />}
          {currentPage === "ramas" && role === "admin" && (
            <BranchesPanel onChange={refresh} />
          )}
          {currentPage === "alertas" && role === "admin" && <AlertsPanel />}
          {currentPage === "visitas" && (
            <HistoryPanel admin={role === "admin"} />
          )}
          {currentPage === "horarios" && (
            <Schedule
              state={state}
              onSelect={(slot) => {
                reserveKey.current = null;
                setSelected(slot);
              }}
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
          {currentPage === "acceso" && <AccessPanel />}
          {currentPage === "usuarios" && role === "admin" && (
            <Members
              state={state}
              onEdit={setEditingMember}
              onToggle={(id, enabled) =>
                void update(
                  () => api.status(Number(id), enabled),
                  "Estado actualizado.",
                )
              }
              onInvitation={(id) =>
                void update(
                  () => api.invitation(Number(id)),
                  "Invitación reenviada.",
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
        <Modal
          title="Confirmar reserva"
          onClose={() => {
            if (!busy) {
              reserveKey.current = null;
              setSelected(null);
            }
          }}
        >
          <div className="confirmation-detail">
            <SlotDetails slot={selected} />
            <p className="meta">
              <MapPin size={16} />
              Sala de pesas UCN
            </p>
          </div>
          <p className="muted">La reserva quedará a nombre de {member.name}.</p>
          <div className="modal-actions">
            <Button
              secondary
              onClick={() => {
                reserveKey.current = null;
                setSelected(null);
              }}
            >
              Volver
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                const body = JSON.stringify({
                  id: selected.id,
                  date: selected.date,
                });
                if (reserveKey.current?.body !== body)
                  reserveKey.current = { body, key: crypto.randomUUID() };
                if (
                  await update(
                    () =>
                      api.reserve(
                        Number(selected.id),
                        selected.date,
                        reserveKey.current!.key,
                      ),
                    "Reserva confirmada. Tu QR ya está disponible.",
                  )
                ) {
                  reserveKey.current = null;
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
              disabled={busy}
              onClick={async () => {
                if (
                  await update(
                    () => api.cancel(Number(toCancel.id)),
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
      {role === "admin" && (showMember || editingMember) && (
        <MemberForm
          initial={editingMember ?? undefined}
          onClose={() => {
            setShowMember(false);
            setEditingMember(null);
          }}
          branches={state.branches}
          onSave={async (data, password) => {
            const firstName = data.firstName ?? "";
            const lastName = data.lastName ?? "";
            try {
              const body = {
                firstName,
                lastName,
                email: data.email,
                role: data.role ?? "Usuario",
                username: data.role === "Usuario" ? undefined : data.username,
                category:
                  data.role && data.role !== "Usuario"
                    ? null
                    : (data.category as import("./api").Category),
                sportBranchId:
                  (!data.role || data.role === "Usuario") &&
                  data.category === "Deportista"
                    ? (data.branchId ?? null)
                    : null,
              };
              if (editingMember)
                await api.updateUser(Number(editingMember.id), body);
              else
                await api.register({
                  ...body,
                  rut: data.rut,
                  ...(password ? { password } : {}),
                });
              refresh();
              setShowMember(false);
              setEditingMember(null);
              return "";
            } catch (cause) {
              return cause instanceof Error
                ? cause.message
                : "No se pudo guardar el usuario.";
            }
          }}
        />
      )}
      {role === "student" &&
        !selected &&
        !toCancel &&
        state.alerts.some((a) => !a.acknowledgedAt) && (
          <Modal
            title="Tienes reservas sin asistencia"
            onClose={() => void acknowledge()}
          >
            <p className="muted">No registramos tu ingreso a estas sesiones:</p>
            <ul className="attendance-alerts">
              {state.alerts
                .filter((a) => !a.acknowledgedAt)
                .map((a) => (
                  <li key={a.id}>
                    {shortDate(a.date)} · {a.startTime} a {a.endTime}
                  </li>
                ))}
            </ul>
            <p className="muted">
              Si necesitas corregir una asistencia, contacta a administración.
            </p>
            <Button disabled={busy} onClick={() => void acknowledge()}>
              Entendido
            </Button>
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
  state: ViewState;
  onSelect: (slot: Slot) => void;
  initialDate: string;
  memberId: string;
}) {
  const days = getDays();
  const [date, setDate] = useState(initialDate);
  const schedules = useApiData(() => api.schedules(date), [date]);
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
        <LoadStatus error={schedules.error} loading={schedules.loading} />
        {(schedules.data ?? []).map(slotFrom).map((slot) => {
          const available = remaining(slot);
          const mine = state.reservations.some(
            (r) =>
              r.memberId === memberId &&
              r.slot.id === slot.id &&
              r.status !== "Cancelado",
          );
          const ended = hasStarted(slot);
          const unavailable = ended ? "Este bloque ya comenzó." : "";
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
                  {slot.capacity - available}/{slot.capacity} ·{" "}
                  {slot.groupSeats} cupos de grupo
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
  admin,
  onCancel,
}: {
  reservations: Reservation[];
  state: ViewState;
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
            <h3>{admin ? r.userName : "Sala de pesas"}</h3>
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
            {r.branch && <small>{r.branch}</small>}
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
          {!admin && ["Reservado", "Asistió"].includes(r.status) && (
            <details className="card">
              <summary>Ver código QR</summary>
              <BlockQr id={Number(r.id)} />
            </details>
          )}
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
  state: ViewState;
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
    "Reservado",
    "Asistió",
    "Cancelado",
    "Inasistencia",
  ];
  const visible = [...scoped]
    .reverse()
    .filter((r) => filter === "Todas" || r.status === filter);
  return (
    <>
      {admin && (
        <SelectField
          label="Rama deportiva"
          fieldClassName="field branch-filter"
          value={branch}
          onChange={(event) => setBranch(event.target.value)}
        >
          <option value="">Todas las ramas</option>
          {branches.map((value) => (
            <option key={value}>{value}</option>
          ))}
        </SelectField>
      )}
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
                ? "Asistidas"
                : f === "Cancelado"
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
}: {
  reservations: Reservation[];
  member: Member;
  onReserve: () => void;
  onCopy: (text: string, error?: boolean) => void;
}) {
  const [selectedId, setSelectedId] = useState("");
  const groups = useApiData(
    () =>
      member.category === "Deportista" ? api.myGroups() : Promise.resolve([]),
    [member.id, member.category],
  );
  const choices = [
    ...reservations.map((r) => ({
      key: `r:${r.id}`,
      id: Number(r.id),
      group: false,
      label: `${shortDate(r.slot.date)} · ${r.slot.start}`,
    })),
    ...(groups.data ?? [])
      .filter((g) => g.status === "Activa")
      .map((g) => ({
        key: `g:${g.id}`,
        id: g.id,
        group: true,
        label: `${g.sportBranch.name} · ${shortDate(g.date)} · ${g.startTime}`,
      })),
  ];
  const selected = choices.find((r) => r.key === selectedId) ?? choices[0];
  if (groups.loading || groups.error)
    return <LoadStatus error={groups.error} loading={groups.loading} />;
  if (!selected)
    return (
      <section className="card">
        <Empty
          title="Primero, una reserva"
          action={<Button onClick={onReserve}>Reservar un horario</Button>}
        >
          Tu código estará disponible al confirmar un bloque.
        </Empty>
      </section>
    );
  return (
    <div className="qr-layout">
      <section className="card qr-card">
        <Brand />
        <SelectField
          label="Reserva"
          fieldClassName="field"
          value={selected.key}
          onChange={(e) => setSelectedId(e.target.value)}
        >
          {choices.map((r) => (
            <option key={r.key} value={r.key}>
              {r.label}
            </option>
          ))}
        </SelectField>
        <BlockQr id={selected.id} group={selected.group} />
      </section>
      <aside className="qr-instructions">
        <h2>Cómo usar tu QR</h2>
        <p>Preséntalo junto a tu Carnet, TNE o credencial universitaria.</p>
        <p>
          Para salir, recepción te selecciona en la lista de personas dentro.
        </p>
      </aside>
    </div>
  );
}
function ResetUserPassword({ id }: { id: number }) {
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <details>
      <summary>Restablecer contraseña</summary>
      <form
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          try {
            setMessage((await api.passwordReset(id, password)).message);
            setPassword("");
          } catch (cause) {
            setMessage(
              cause instanceof Error
                ? cause.message
                : "No se pudo restablecer.",
            );
          } finally {
            setBusy(false);
          }
        }}
      >
        <label className="field">
          Contraseña temporal
          <input
            required
            type="password"
            minLength={8}
            maxLength={128}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        <button className="button" disabled={busy}>
          Restablecer
        </button>
        <p role="status">{message}</p>
      </form>
    </details>
  );
}

function Members({
  state,
  onToggle,
  onEdit,
  onInvitation,
}: {
  state: ViewState;
  onToggle: (id: string, enabled: boolean) => void;
  onEdit: (member: Member) => void;
  onInvitation: (id: string) => void;
}) {
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState("Todos");
  const [branch, setBranch] = useState("");
  const [category, setCategory] = useState("");
  const branches = state.branches.map((branch) => branch.name).sort();
  const remoteUsers = useApiData(
    () =>
      api.users({
        search,
        category,
        branchId: state.branches.find((b) => b.name === branch)?.id,
        enabled: filter === "Todos" ? undefined : filter === "Habilitados",
      }),
    [search, category, branch, filter, state],
  );
  const members = (remoteUsers.data ?? [])
    .map(memberFrom)
    .sort((a, b) => a.name.localeCompare(b.name));
  return (
    <section className="card members-card">
      <LoadStatus error={remoteUsers.error} loading={remoteUsers.loading} />
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
                  {m.accountStatus === "Pendiente" && (
                    <button
                      className="text-button"
                      onClick={() => onInvitation(m.id)}
                    >
                      Reenviar invitación
                    </button>
                  )}
                  <ResetUserPassword id={Number(m.id)} />
                </td>
                <td>
                  <button
                    className={`toggle ${m.enabled ? "enabled" : ""}`}
                    role="switch"
                    aria-checked={m.enabled}
                    aria-label={`Habilitar acceso de ${m.name}`}
                    onClick={() => onToggle(m.id, !m.enabled)}
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
  branches,
}: {
  onSave: (
    data: Omit<Member, "id" | "enabled">,
    password?: string,
  ) => Promise<string>;
  onClose: () => void;
  initial?: Member;
  branches: import("./api").BranchResponse[];
}) {
  const [firstName, setFirstName] = useState(initial?.firstName ?? "");
  const [lastName, setLastName] = useState(initial?.lastName ?? "");
  const [rut, setRut] = useState(initial?.rut ?? "");
  const [email, setEmail] = useState(initial?.email ?? "");
  const [category, setCategory] = useState(initial?.category ?? "Deportista");
  const [branch, setBranch] = useState(String(initial?.branchId ?? ""));
  const [password, setPassword] = useState("");
  const [memberRole, setMemberRole] = useState<import("./api").Role>(
    initial?.role ?? "Usuario",
  );
  const [username, setUsername] = useState(initial?.username ?? "");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!firstName.trim() || !lastName.trim()) {
      setError("Ingresa nombre y apellido.");
      return;
    }
    if (!isValidRut(rut)) {
      setError("Ingresa un RUT válido con su dígito verificador.");
      return;
    }
    if (
      memberRole === "Usuario" &&
      category === "Deportista" &&
      !branch.trim()
    ) {
      setError("Indica la rama deportiva.");
      return;
    }
    setSaving(true);
    setError(
      await onSave(
        {
          name: `${firstName.trim()} ${lastName.trim()}`,
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          role: memberRole,
          username,
          rut: formatRut(rut),
          email,
          category,
          branchId:
            memberRole === "Usuario" && category === "Deportista"
              ? Number(branch)
              : undefined,
        },
        password,
      ),
    );
    setSaving(false);
  }
  return (
    <Modal
      title={initial ? "Editar usuario" : "Agregar usuario"}
      onClose={onClose}
    >
      <form onSubmit={submit} className="member-form">
        <SelectField
          label="Rol"
          fieldClassName="field"
          value={memberRole}
          onChange={(e) =>
            setMemberRole(e.target.value as import("./api").Role)
          }
        >
          {["Usuario", "Admin", "Recepcionista"].map((r) => (
            <option key={r}>{r}</option>
          ))}
        </SelectField>
        {memberRole !== "Usuario" && (
          <label className="field">
            Nombre de usuario
            <input
              required
              value={username}
              onChange={(e) => setUsername(e.target.value)}
            />
          </label>
        )}
        {!initial && (
          <label className="field">
            Contraseña temporal (opcional)
            <input
              type="password"
              minLength={8}
              maxLength={128}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="new-password"
            />
          </label>
        )}
        <label className="field">
          Nombre
          <input
            autoFocus
            value={firstName}
            onChange={(e) => setFirstName(e.target.value)}
            required
            maxLength={100}
            autoComplete="off"
          />
        </label>
        <label className="field">
          Apellido
          <input
            required
            maxLength={100}
            value={lastName}
            onChange={(e) => setLastName(e.target.value)}
            autoComplete="off"
          />
        </label>
        <label className="field">
          RUT
          <input
            readOnly={!!initial}
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
        <SelectField
          label="Categoría"
          fieldClassName="field"
          value={category}
          onChange={(e) => setCategory(e.target.value)}
        >
          {CATEGORIES.map((c) => (
            <option key={c}>{c}</option>
          ))}
        </SelectField>
        {memberRole === "Usuario" && category === "Deportista" && (
          <SelectField
            label="Rama deportiva"
            fieldClassName="field"
            required
            value={branch}
            onChange={(e) => setBranch(e.target.value)}
          >
            <option value="">Selecciona una rama</option>
            {branches
              .filter((b) => b.enabled || b.id === initial?.branchId)
              .map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
          </SelectField>
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
          <button type="submit" className="button" disabled={saving}>
            {initial ? "Guardar cambios" : "Agregar usuario"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

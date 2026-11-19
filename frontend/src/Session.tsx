import { useEffect, useState, type FormEvent, type ReactNode } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { Dumbbell } from "lucide-react";
import {
  api,
  ApiError,
  onPasswordRequired,
  onUnauthorized,
  type UserResponse,
} from "./api";
import AccountPage from "./pages/AccountPage";
export default function Session({
  children,
}: {
  children: (user: UserResponse) => ReactNode;
}) {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [required, setRequired] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();
  useEffect(() => {
    let active = true;
    const off = onUnauthorized(() => {
      setUser(null);
      setRequired(false);
      navigate("/", { replace: true });
    });
    const offPassword = onPasswordRequired(() => setRequired(true));
    api
      .me()
      .then((u) => {
        if (active) setUser(u);
      })
      .catch((cause) => {
        if (active && (!(cause instanceof ApiError) || cause.status !== 401))
          setError(cause.message);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
      off();
      offPassword();
    };
  }, []);
  const accountMode =
    location.pathname === "/recuperar-contrasena"
      ? "recover"
      : location.pathname === "/activar-cuenta"
        ? "activate"
        : location.pathname === "/restablecer-contrasena"
          ? "reset"
          : null;
  // A temporary password blocks every authenticated destination, including crafted URLs.
  if (user && (user.mustChangePassword || required))
    return (
      <ChangePassword
        onChanged={(u) => {
          setUser(u);
          setRequired(false);
          navigate("/", { replace: true });
        }}
      />
    );
  if (accountMode)
    return (
      <AccountPage
        key={location.pathname}
        mode={accountMode}
        onBack={() => navigate("/")}
      />
    );
  if (loading)
    return (
      <main className="account-page">
        <p role="status">Comprobando sesión…</p>
      </main>
    );
  if (user) return children(user);
  return (
    <Login
      error={error}
      onLogin={(u) => {
        setError("");
        setUser(u);
      }}
      onRecover={() => navigate("/recuperar-contrasena")}
    />
  );
}
function Login({
  error: initialError,
  onLogin,
  onRecover,
}: {
  error: string;
  onLogin: (u: UserResponse) => void;
  onRecover: () => void;
}) {
  const [staff, setStaff] = useState(false);
  const [identifier, setIdentifier] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(initialError);
  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      const result = await api.login(staff, identifier, password);
      setPassword("");
      onLogin(result.user);
    } catch (cause) {
      setError(
        cause instanceof Error ? cause.message : "No se pudo iniciar sesión.",
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="login-page">
      <section className="login-story" aria-label="Deportes UCN">
        <div className="brand inverse">
          <img
            src="/deportes-ucn.png"
            width="64"
            height="64"
            alt="Deportes UCN"
          />
          <span className="brand-caption">
            <strong>UCN Gym</strong>
            <span>Sala de pesas</span>
          </span>
        </div>
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
            <Dumbbell size={20} />
            Sala de pesas · Universidad Católica del Norte
          </span>
        </div>
      </section>
      <main className="login-main">
        <form className="login-form" onSubmit={submit}>
          <span className="eyebrow">UNIVERSIDAD CATÓLICA DEL NORTE</span>
          <h2>Accede a tu espacio</h2>
          <p className="intro">Selecciona tu portal.</p>
          <label className="field">
            Portal
            <select
              value={staff ? "staff" : "user"}
              onChange={(e) => {
                setStaff(e.target.value === "staff");
                setIdentifier("");
              }}
            >
              <option value="user">Usuario / Estudiante</option>
              <option value="staff">
                Personal · Administrador / Recepcionista
              </option>
            </select>
          </label>
          <label className="field">
            {staff ? "Nombre de usuario" : "RUT"}
            <input
              autoComplete="username"
              required
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
            />
          </label>
          <label className="field">
            Contraseña
            <input
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </label>
          {error && (
            <p role="alert" className="form-error">
              {error}
            </p>
          )}
          <button className="button" disabled={busy}>
            {busy ? "Ingresando…" : "Ingresar"}
          </button>
          <button type="button" className="text-button" onClick={onRecover}>
            Olvidé mi contraseña
          </button>
        </form>
        <span className="login-footer">Deportes UCN · Sala de pesas</span>
      </main>
    </div>
  );
}
function ChangePassword({
  onChanged,
}: {
  onChanged: (u: UserResponse) => void;
}) {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <main className="account-page">
      <section className="card account-card">
        <h1>Cambiar contraseña temporal</h1>
        <p>Debes cambiar tu contraseña para continuar.</p>
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            if (next !== confirm) {
              setError("Las contraseñas no coinciden.");
              return;
            }
            setBusy(true);
            setError("");
            try {
              const result = await api.changePassword(current, next);
              setCurrent("");
              setNext("");
              setConfirm("");
              onChanged(result.user);
            } catch (cause) {
              setError(
                cause instanceof Error
                  ? cause.message
                  : "No se pudo cambiar la contraseña.",
              );
            } finally {
              setBusy(false);
            }
          }}
        >
          <label className="field">
            Contraseña actual
            <input
              required
              type="password"
              autoComplete="current-password"
              value={current}
              onChange={(e) => setCurrent(e.target.value)}
            />
          </label>
          <label className="field">
            Nueva contraseña
            <input
              required
              minLength={8}
              maxLength={128}
              type="password"
              autoComplete="new-password"
              value={next}
              onChange={(e) => setNext(e.target.value)}
            />
          </label>
          <label className="field">
            Repetir contraseña
            <input
              required
              minLength={8}
              maxLength={128}
              type="password"
              autoComplete="new-password"
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
            />
          </label>
          {error && <p role="alert">{error}</p>}
          <button className="button" disabled={busy}>
            Guardar contraseña
          </button>
        </form>
      </section>
    </main>
  );
}

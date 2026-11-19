import { api } from "../api";
import { useEffect, useState, type FormEvent } from "react";
import { ArrowLeft, KeyRound, Mail } from "lucide-react";

export default function AccountPage({
  mode,
  onBack,
}: {
  mode: "recover" | "activate" | "reset";
  onBack: () => void;
}) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const [token] = useState(
    () => new URLSearchParams(window.location.search).get("token") ?? "",
  );
  useEffect(() => {
    const url = new URL(window.location.href);
    url.searchParams.delete("token");
    window.history.replaceState(
      window.history.state,
      "",
      url.pathname + url.search + url.hash,
    );
  }, []);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setError("");
    if (mode !== "recover" && (!token || token.length > 4096)) {
      setError("El enlace no contiene un token válido. Solicita uno nuevo.");
      return;
    }
    if (mode !== "recover" && password !== confirm) {
      setError("Las contraseñas no coinciden.");
      return;
    }
    setBusy(true);
    try {
      if (mode === "recover") await api.recover(email);
      else if (mode === "activate") await api.activate(token, password);
      else await api.reset(token, password);
      setPassword("");
      setConfirm("");
      setSuccess(true);
    } catch (cause) {
      if (mode === "recover") {
        setSuccess(true);
        return;
      }
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo conectar al servicio de cuentas.",
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="account-page">
      <section className="card account-card">
        <img
          src="/deportes-ucn.png"
          width="84"
          height="84"
          alt="Deportes UCN"
        />
        <span className="option-icon">
          {mode === "recover" ? <Mail /> : <KeyRound />}
        </span>
        <h1>
          {mode === "recover"
            ? "Recuperar contraseña"
            : mode === "activate"
              ? "Activa tu cuenta"
              : "Nueva contraseña"}
        </h1>
        {success ? (
          <p role="status">
            {mode === "recover"
              ? "Si el correo está registrado, recibirás un enlace para restablecer tu contraseña."
              : "La contraseña se actualizó correctamente."}
          </p>
        ) : (
          <form onSubmit={(event) => void submit(event)}>
            {mode === "recover" ? (
              <label className="field">
                Correo registrado
                <input
                  type="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  autoComplete="email"
                  required
                  maxLength={200}
                />
              </label>
            ) : (
              <>
                <label className="field">
                  Contraseña
                  <input
                    type="password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    autoComplete="new-password"
                    minLength={8}
                    maxLength={128}
                    required
                  />
                </label>
                <p className="muted">Usa entre 8 y 128 caracteres.</p>
                <label className="field">
                  Repetir contraseña
                  <input
                    type="password"
                    value={confirm}
                    onChange={(event) => setConfirm(event.target.value)}
                    autoComplete="new-password"
                    minLength={8}
                    maxLength={128}
                    required
                  />
                </label>
              </>
            )}
            {error && (
              <p role="alert" className="form-error">
                {error}
              </p>
            )}
            <button className="button" disabled={busy} type="submit">
              {busy
                ? "Procesando…"
                : mode === "recover"
                  ? "Enviar enlace"
                  : "Guardar contraseña"}
            </button>
          </form>
        )}
        <button className="text-button" onClick={onBack}>
          <ArrowLeft size={16} />
          Volver al inicio
        </button>
      </section>
    </main>
  );
}

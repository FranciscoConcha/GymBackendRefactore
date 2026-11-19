import { lazy, Suspense, useRef, useState } from "react";
import { ArrowDownLeft, ArrowUpRight, ScanLine } from "lucide-react";
import { api, type AccessValidation, type DocumentType } from "../api";
import { formatRut, shortDate } from "../domain";
import { LoadStatus, useApiData } from "../remote";
const Camera = lazy(() => import("../Camera"));
export default function AccessPanel() {
  const [action, setAction] = useState<"entry" | "exit">("entry");
  const [code, setCode] = useState("");
  const [camera, setCamera] = useState(false);
  const [validation, setValidation] = useState<AccessValidation>();
  const [documentType, setDocumentType] =
    useState<DocumentType>("CarnetIdentidad");
  const [verified, setVerified] = useState(false);
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [exitUser, setExitUser] = useState("");
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const sending = useRef(false);
  const generation = useRef(0);
  const current = useApiData(() => api.current(), [revision]);
  function read(value: string) {
    generation.current++;
    setCode(value);
    setValidation(undefined);
    setVerified(false);
    setMessage("");
    setError("");
    attempt.current = null;
  }
  async function perform(operation: () => Promise<void>) {
    if (sending.current) return;
    sending.current = true;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      await operation();
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo registrar el acceso.",
      );
    } finally {
      sending.current = false;
      setBusy(false);
    }
  }
  return (
    <div className="access-layout">
      <section className="card scanner-card">
        <div className="access-tabs">
          <button
            disabled={busy}
            aria-pressed={action === "entry"}
            onClick={() => {
              setAction("entry");
              read("");
            }}
          >
            <ArrowDownLeft size={19} />
            Entrada
          </button>
          <button
            disabled={busy}
            aria-pressed={action === "exit"}
            onClick={() => {
              setAction("exit");
              setCamera(false);
              read("");
            }}
          >
            <ArrowUpRight size={19} />
            Salida
          </button>
        </div>
        {action === "entry" ? (
          <>
            <div className="scan-area">
              {camera ? (
                <Suspense fallback={<p>Abriendo cámara…</p>}>
                  <Camera
                    onRead={(value) => {
                      setCamera(false);
                      read(value);
                    }}
                  />
                </Suspense>
              ) : (
                <>
                  <ScanLine size={64} strokeWidth={1.3} />
                  <h3>Acerca el código QR</h3>
                  <p>Escanea y comprueba la identidad antes de confirmar.</p>
                </>
              )}
            </div>
            <button
              type="button"
              className="button secondary"
              disabled={busy}
              onClick={() => setCamera(!camera)}
            >
              {camera ? "Cerrar cámara" : "Activar cámara"}
            </button>
            <div className="or-divider">
              <span>o ingresa el código</span>
            </div>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const version = generation.current;
                setValidation(undefined);
                setVerified(false);
                void perform(async () => {
                  const result = await api.validate(code);
                  if (version !== generation.current) return;
                  setValidation(result);
                  setVerified(false);
                });
              }}
            >
              <label className="field">
                Código de la reserva
                <input
                  required
                  value={code}
                  disabled={busy}
                  onChange={(e) => read(e.target.value)}
                />
              </label>
              <button className="button" disabled={busy || !code.trim()}>
                Validar código
              </button>
            </form>
            {validation && (
              <div className="identity-review">
                <span className="eyebrow">COMPROBAR IDENTIDAD</span>
                <h3>
                  {validation.user.firstName} {validation.user.lastName}
                </h3>
                <strong>{formatRut(validation.user.rut)}</strong>
                <p>
                  {validation.user.category} ·{" "}
                  {validation.user.sportBranch?.name}
                </p>
                {validation.session && (
                  <p>
                    {shortDate(validation.session.date)} ·{" "}
                    {validation.session.startTime} a{" "}
                    {validation.session.endTime}
                  </p>
                )}
                <p>
                  Aforo: {validation.occupancy.current} /{" "}
                  {validation.occupancy.max}
                </p>
                {validation.reason && <p role="status">{validation.reason}</p>}
                <label className="field">
                  Documento presentado
                  <select
                    value={documentType}
                    disabled={busy}
                    onChange={(e) => {
                      setDocumentType(e.target.value as DocumentType);
                      setVerified(false);
                      attempt.current = null;
                    }}
                  >
                    <option value="CarnetIdentidad">Carnet</option>
                    <option value="TNE">TNE</option>
                    <option value="CredencialUniversitaria">
                      Credencial universitaria
                    </option>
                  </select>
                </label>
                <label className="identity-check">
                  <input
                    type="checkbox"
                    checked={verified}
                    disabled={busy}
                    onChange={(e) => setVerified(e.target.checked)}
                  />
                  Confirmo que el nombre y RUT coinciden con el documento
                  presentado.
                </label>
                <button
                  className="button"
                  disabled={
                    busy ||
                    !verified ||
                    !validation.canEnter ||
                    validation.inside
                  }
                  onClick={() =>
                    void perform(async () => {
                      const body = JSON.stringify({
                        token: code,
                        documentType,
                      });
                      if (attempt.current?.body !== body)
                        attempt.current = { body, key: crypto.randomUUID() };
                      const result = await api.entry(
                        code,
                        documentType,
                        attempt.current.key,
                      );
                      setMessage(result.message);
                      setValidation(undefined);
                      setVerified(false);
                      setCode("");
                      attempt.current = null;
                      setRevision((v) => v + 1);
                    })
                  }
                >
                  Registrar entrada
                </button>
              </div>
            )}
          </>
        ) : (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              void perform(async () => {
                const result = await api.exit(Number(exitUser));
                setMessage(result.message);
                setExitUser("");
                setRevision((v) => v + 1);
              });
            }}
          >
            <label className="field">
              Persona dentro
              <select
                required
                value={exitUser}
                onChange={(e) => setExitUser(e.target.value)}
              >
                <option value="">Selecciona una persona</option>
                {current.data?.people.map((p) => (
                  <option key={p.userId} value={p.userId}>
                    {p.fullName} · {p.rut}
                  </option>
                ))}
              </select>
            </label>
            <button className="button" disabled={busy || !exitUser}>
              Registrar salida
            </button>
          </form>
        )}
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        {message && <p role="status">{message}</p>}
      </section>
      <aside>
        <section className="card access-summary">
          <div className="section-title">
            <h2>En sala</h2>
            {current.data && (
              <span>
                {current.data.occupancy.current} / {current.data.occupancy.max}
              </span>
            )}
          </div>
          <LoadStatus error={current.error} loading={current.loading} />
          <button
            className="text-button"
            onClick={() => setRevision((v) => v + 1)}
          >
            Actualizar estado
          </button>
          {current.data?.people.map((p) => (
            <div className="present-member" key={p.userId}>
              <span className="avatar">{p.fullName.slice(0, 1)}</span>
              <div>
                <strong>{p.fullName}</strong>
                <span>
                  {formatRut(p.rut)} · {p.category}
                </span>
                <span>Fin de sesión: {p.sessionEndTime}</span>
              </div>
            </div>
          ))}
          {current.data?.people.length === 0 && (
            <p className="muted">Sin entradas abiertas.</p>
          )}
        </section>
      </aside>
    </div>
  );
}

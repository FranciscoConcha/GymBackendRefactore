import { lazy, Suspense, useState } from "react";
import {
  ArrowDownLeft,
  ArrowRight,
  ArrowUpRight,
  CheckCircle2,
  ChevronRight,
  ScanLine,
} from "lucide-react";
import { Badge, Button } from "../components/ui";
import {
  formatRut,
  isPresent,
  presentReservations,
  reservationFromCode,
  ROOM_CAPACITY,
  shortDate,
  slotEnd,
  slotStart,
  ticket,
  type DemoState,
  type IdentityDocument,
  type Role,
} from "../domain";

const Camera = lazy(() => import("../Camera"));
export default function AccessPanel({
  state,
  role,
  onSubmit,
}: {
  state: DemoState;
  role: Role;
  onSubmit: (
    code: string,
    action: "entry" | "exit",
    document: IdentityDocument,
  ) => boolean;
}) {
  const [action, setAction] = useState<"entry" | "exit">("entry");
  const [code, setCode] = useState("");
  const [camera, setCamera] = useState(false);
  const [success, setSuccess] = useState(false);
  const [document, setDocument] = useState<IdentityDocument>("Carnet");
  const [verified, setVerified] = useState(false);
  const present = presentReservations(state);
  let reservation;
  try {
    reservation = code.trim() ? reservationFromCode(state, code) : undefined;
  } catch {
    /* Shown inline below. */
  }
  const member =
    reservation &&
    state.members.find((value) => value.id === reservation.memberId);
  const now = Date.now();
  const candidates = state.reservations.filter((value) =>
    action === "exit"
      ? isPresent(value)
      : ["Reservada", "Fuera de sala"].includes(value.status) &&
        slotStart(value.slot).getTime() - 900000 <= now &&
        slotEnd(value.slot).getTime() > now,
  );
  function read(value: string) {
    setCode(value);
    setSuccess(false);
    setVerified(false);
  }
  function submit() {
    if (action === "entry" && !verified) return;
    setSuccess(onSubmit(code, action, document));
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
              setVerified(false);
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
              setVerified(false);
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
                  read(value);
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
                  ? "El estado actual ya está actualizado."
                  : "Escanea y comprueba la identidad antes de confirmar."}
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
          onSubmit={(event) => {
            event.preventDefault();
            submit();
          }}
        >
          <label className="field">
            Código de la reserva
            <input
              value={code}
              onChange={(event) => read(event.target.value)}
              placeholder="UCN:…"
              required
            />
          </label>
          {code.trim() && !reservation && (
            <p className="form-error">
              No encontramos una reserva para este código.
            </p>
          )}
          {member && reservation && (
            <div className="identity-review">
              <span className="eyebrow">COMPROBAR IDENTIDAD</span>
              <h3>{member.name}</h3>
              <strong>{formatRut(member.rut) || "RUT pendiente"}</strong>
              <p>{member.branch ?? member.category}</p>
              <p>
                {shortDate(reservation.slot.date)} · {reservation.slot.start} a{" "}
                {reservation.slot.end}
              </p>
              {action === "entry" && (
                <>
                  <label className="field">
                    Documento presentado
                    <select
                      aria-label="Documento presentado"
                      value={document}
                      onChange={(event) => {
                        setDocument(event.target.value as IdentityDocument);
                        setVerified(false);
                      }}
                    >
                      {["Carnet", "TNE", "Credencial universitaria"].map(
                        (value) => (
                          <option key={value}>{value}</option>
                        ),
                      )}
                    </select>
                  </label>
                  <label className="identity-check">
                    <input
                      type="checkbox"
                      checked={verified}
                      onChange={(event) => setVerified(event.target.checked)}
                    />
                    Confirmo que el nombre y RUT coinciden con el documento
                    presentado.
                  </label>
                </>
              )}
            </div>
          )}
          <button
            className="button"
            type="submit"
            disabled={!reservation || (action === "entry" && !verified)}
          >
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
              {present.length} / {ROOM_CAPACITY}
            </Badge>
          </div>
          {present.map((value) => {
            const person = state.members.find(
              (item) => item.id === value.memberId,
            );
            return (
              <div className="present-member" key={value.id}>
                <span className="avatar">{person?.name.slice(0, 1)}</span>
                <div>
                  <strong>{person?.name}</strong>
                  <span>
                    {formatRut(person?.rut ?? "")} ·{" "}
                    {person?.branch ?? person?.category}
                  </span>
                  <span>
                    Bloque {value.slot.start} a {value.slot.end}
                  </span>
                </div>
              </div>
            );
          })}
          {!present.length && <p className="muted">Sin entradas abiertas.</p>}
        </section>
        {role === "admin" && (
          <section className="card reservation-lookup">
            <span className="eyebrow">BÚSQUEDA MANUAL</span>
            <h3>
              {action === "entry" ? "Sesiones vigentes" : "Visitas abiertas"}
            </h3>
            <p>Selecciona una persona para cargar su reserva.</p>
            {candidates.length ? (
              candidates.map((value) => (
                <button
                  className="lookup-reservation"
                  key={value.id}
                  onClick={() => read(ticket(value.id))}
                >
                  <span>
                    <strong>
                      {
                        state.members.find(
                          (person) => person.id === value.memberId,
                        )?.name
                      }
                    </strong>
                    <small>
                      {shortDate(value.slot.date, {
                        day: "numeric",
                        month: "short",
                      })}{" "}
                      · {value.slot.start}
                    </small>
                  </span>
                  <ChevronRight size={18} />
                </button>
              ))
            ) : (
              <p className="muted">
                No hay sesiones disponibles para esta operación.
              </p>
            )}
          </section>
        )}
      </aside>
    </div>
  );
}

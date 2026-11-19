import { useRef, useState } from "react";
import { CalendarDays, Users } from "lucide-react";
import { api, type GroupBookingRequest } from "../api";
import { getDays, hasStarted, remaining, shortDate, slotFrom } from "../domain";
import { LoadStatus, useApiData } from "../remote";
import BlockQr from "./BlockQr";
export default function GroupBooking({ admin }: { admin: boolean }) {
  const [revision, setRevision] = useState(0);
  const [branch, setBranch] = useState("");
  const [date, setDate] = useState(getDays()[0]);
  const [slotId, setSlotId] = useState("");
  const [mode, setMode] = useState<GroupBookingRequest["mode"]>("Cupos");
  const [seats, setSeats] = useState(1);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [qr, setQr] = useState<number>();
  const attempt = useRef<{ body: string; key: string } | null>(null);
  const sending = useRef(false);
  const branches = useApiData(
    () => (admin ? api.branches() : Promise.resolve([])),
    [admin],
  );
  const schedules = useApiData(
    () => (admin ? api.schedules(date) : Promise.resolve([])),
    [admin, date, revision],
  );
  const groups = useApiData(
    () => (admin ? api.groups() : api.myGroups()),
    [admin, revision],
  );
  const mutate = async (operation: () => Promise<unknown>) => {
    if (sending.current) return;
    sending.current = true;
    setBusy(true);
    setError("");
    try {
      await operation();
      setRevision((v) => v + 1);
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo completar la acción.",
      );
    } finally {
      sending.current = false;
      setBusy(false);
    }
  };
  return (
    <div className="group-layout">
      {admin && (
        <form
          className="card group-form"
          onSubmit={(e) => {
            e.preventDefault();
            const body: GroupBookingRequest = {
              sportBranchId: Number(branch),
              scheduleSlotId: Number(slotId),
              date,
              mode,
              ...(mode === "Cupos" ? { seats } : {}),
            };
            const serialized = JSON.stringify(body);
            if (attempt.current?.body !== serialized)
              attempt.current = { body: serialized, key: crypto.randomUUID() };
            void mutate(async () => {
              await api.createGroup(body, attempt.current!.key);
              attempt.current = null;
              setSlotId("");
            });
          }}
        >
          <div className="section-title">
            <h2>Reservar para una rama</h2>
            <Users size={21} />
          </div>
          <LoadStatus
            error={branches.error || schedules.error}
            loading={branches.loading || schedules.loading}
          />
          <div className="form-grid">
            <label className="field">
              Rama deportiva
              <select
                required
                value={branch}
                onChange={(e) => {
                  setBranch(e.target.value);
                  attempt.current = null;
                }}
              >
                <option value="">Selecciona una rama</option>
                {branches.data
                  ?.filter((b) => b.enabled)
                  .map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name}
                    </option>
                  ))}
              </select>
            </label>
            <label className="field">
              Fecha
              <input
                required
                type="date"
                value={date}
                onChange={(e) => {
                  setDate(e.target.value);
                  setSlotId("");
                  attempt.current = null;
                }}
              />
            </label>
          </div>
          <label className="field">
            Horario
            <select
              required
              value={slotId}
              onChange={(e) => {
                setSlotId(e.target.value);
                attempt.current = null;
              }}
            >
              <option value="">Selecciona un bloque</option>
              {schedules.data?.map(slotFrom).map((s) => (
                <option value={s.id} key={s.id} disabled={hasStarted(s)}>
                  {s.start} a {s.end} · {remaining(s)} cupos
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            Modo
            <select
              value={mode}
              onChange={(e) => {
                setMode(e.target.value as GroupBookingRequest["mode"]);
                attempt.current = null;
              }}
            >
              <option value="Cupos">Por cantidad de cupos</option>
              <option value="TotalDisponible">Todo el cupo disponible</option>
            </select>
          </label>
          {mode === "Cupos" && (
            <label className="field">
              Cantidad de cupos
              <input
                required
                type="number"
                min={1}
                step={1}
                value={seats}
                onChange={(e) => {
                  setSeats(Number(e.target.value));
                  attempt.current = null;
                }}
              />
            </label>
          )}
          <p className="muted">
            Anticipación mínima: 1 hora. Los cupos son para deportistas de la
            rama.
          </p>
          <button className="button" disabled={busy}>
            <CalendarDays size={18} />
            Confirmar sesión de rama
          </button>
        </form>
      )}
      <section className="card">
        <div className="section-title">
          <h2>Sesiones de rama</h2>
          <span>{groups.data?.length ?? 0}</span>
        </div>
        <LoadStatus error={groups.error} loading={groups.loading} />
        {groups.data?.map((g) => (
          <article className="group-record" key={g.id}>
            <strong>{g.sportBranch.name}</strong>
            <p>
              {shortDate(g.date)} · {g.startTime} a {g.endTime}
            </p>
            <small>
              {g.seats} cupos · {g.usedSeats} usados · {g.status}
            </small>
            {admin && g.status === "Activa" && (
              <button
                className="text-button"
                disabled={busy}
                onClick={() => void mutate(() => api.cancelGroup(g.id))}
              >
                Cancelar grupo
              </button>
            )}
            {!admin && (
              <button
                className="text-button"
                onClick={() => setQr(qr === g.id ? undefined : g.id)}
              >
                Ver código QR del grupo
              </button>
            )}
            {qr === g.id && <BlockQr id={g.id} group />}
          </article>
        ))}
        {groups.data?.length === 0 && (
          <div className="panel-empty">
            <Users size={28} />
            <h3>Sin sesiones de rama</h3>
            <p>Las reservas confirmadas aparecerán aquí.</p>
          </div>
        )}
      </section>
      {error && (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
    </div>
  );
}

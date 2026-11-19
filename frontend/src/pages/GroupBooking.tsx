import { useState, type FormEvent } from "react";
import { CalendarDays, Users } from "lucide-react";
import {
  getDays,
  remaining,
  shortDate,
  slotStart,
  slotsFor,
  type DemoState,
  type Slot,
} from "../domain";

export default function GroupBooking({
  state,
  onReserve,
}: {
  state: DemoState;
  onReserve: (branch: string, ids: string[], slot: Slot) => boolean;
}) {
  const branches = [
    ...new Set(
      state.members
        .filter((member) => member.category === "Deportista" && member.branch)
        .map((member) => member.branch!),
    ),
  ].sort();
  const [branch, setBranch] = useState(branches[0] ?? "");
  const [date, setDate] = useState(
    () =>
      getDays().find((day) =>
        slotsFor(day).some(
          (slot) =>
            slotStart(slot).getTime() - Date.now() >= 3600000 &&
            remaining(slot, state) > 0,
        ),
      ) ?? getDays()[0],
  );
  const [slotId, setSlotId] = useState("");
  const [selected, setSelected] = useState<string[]>([]);
  const [error, setError] = useState("");
  const slots = slotsFor(date);
  const slot = slots.find((value) => value.id === slotId);
  const members = state.members.filter(
    (member) =>
      member.category === "Deportista" &&
      member.branch === branch &&
      member.enabled &&
      member.accountStatus !== "Pendiente de activación",
  );
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!slot) {
      setError("Selecciona un horario.");
      return;
    }
    if (!selected.length) {
      setError("Selecciona al menos un deportista.");
      return;
    }
    if (onReserve(branch, selected, slot)) {
      setSelected([]);
      setError("");
    }
  }
  return (
    <div className="group-layout">
      <form className="card group-form" onSubmit={submit}>
        <div className="section-title">
          <h2>Reservar para una rama</h2>
          <Users size={21} />
        </div>
        <div className="form-grid">
          <label className="field">
            Rama deportiva
            <select
              aria-label="Rama deportiva"
              value={branch}
              onChange={(event) => {
                setBranch(event.target.value);
                setSelected([]);
              }}
            >
              {branches.map((value) => (
                <option key={value}>{value}</option>
              ))}
            </select>
          </label>
          <label className="field">
            Fecha
            <select
              aria-label="Fecha"
              value={date}
              onChange={(event) => {
                setDate(event.target.value);
                setSlotId("");
              }}
            >
              {getDays().map((day) => (
                <option value={day} key={day}>
                  {shortDate(day)}
                </option>
              ))}
            </select>
          </label>
        </div>
        <label className="field">
          Horario
          <select
            aria-label="Horario"
            value={slotId}
            onChange={(event) => setSlotId(event.target.value)}
            required
          >
            <option value="">Selecciona un bloque</option>
            {slots.map((value) => {
              const earlyEnough =
                slotStart(value).getTime() - Date.now() >= 3600000;
              return (
                <option
                  value={value.id}
                  key={value.id}
                  disabled={!earlyEnough || !remaining(value, state)}
                >
                  {value.start} a {value.end} · {remaining(value, state)} cupos
                  {!earlyEnough ? " · Menos de 1 h de anticipación" : ""}
                </option>
              );
            })}
          </select>
        </label>
        <fieldset className="participants">
          <legend>Deportistas de {branch || "la rama"}</legend>
          {members.map((member) => (
            <label key={member.id}>
              <input
                type="checkbox"
                checked={selected.includes(member.id)}
                onChange={(event) =>
                  setSelected((ids) =>
                    event.target.checked
                      ? [...ids, member.id]
                      : ids.filter((id) => id !== member.id),
                  )
                }
              />
              <span>
                <strong>{member.name}</strong>
                <small>{member.rut}</small>
              </span>
            </label>
          ))}
          {!members.length && (
            <p className="muted">
              No hay deportistas habilitados en esta rama.
            </p>
          )}
        </fieldset>
        <div className="group-summary">
          <span>{selected.length} participantes</span>
          <strong>
            {slot
              ? `${remaining(slot, state)} cupos disponibles`
              : "Anticipación mínima: 1 hora"}
          </strong>
        </div>
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <button
          className="button"
          type="submit"
          disabled={
            !slot ||
            !selected.length ||
            selected.length > remaining(slot, state)
          }
        >
          <CalendarDays size={18} />
          Confirmar sesión de rama
        </button>
      </form>
      <section className="card">
        <div className="section-title">
          <h2>Sesiones de rama</h2>
          <span>{state.groups?.length ?? 0}</span>
        </div>
        {state.groups?.length ? (
          [...state.groups].reverse().map((group) => (
            <article className="group-record" key={group.id}>
              <strong>{group.branch}</strong>
              <p>
                {shortDate(group.slot.date)} · {group.slot.start}
              </p>
              <small>
                {group.memberIds.length} deportistas · Cada participante tiene
                su propia reserva y QR
              </small>
            </article>
          ))
        ) : (
          <div className="panel-empty">
            <Users size={28} />
            <h3>Sin sesiones de rama</h3>
            <p>Las reservas confirmadas aparecerán aquí.</p>
          </div>
        )}
      </section>
    </div>
  );
}

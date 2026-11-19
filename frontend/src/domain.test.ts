import { beforeEach, describe, expect, it } from "vitest";
import {
  access,
  cancel,
  DEMO_MEMBER,
  getDays,
  initialState,
  loadDemo,
  remaining,
  reserve,
  slotsFor,
  STORAGE_KEY,
  ticket,
} from "./domain";

describe("flujo de demostración", () => {
  beforeEach(() => localStorage.clear());
  const slot = slotsFor("2026-11-13")[0];
  it("reserva un bloque y descuenta un cupo", () => {
    const state = reserve(structuredClone(initialState), DEMO_MEMBER, slot);
    expect(state.reservations[0].status).toBe("Reservada");
    expect(remaining(slot, state)).toBe(12);
  });
  it("rechaza una reserva duplicada", () => {
    const state = reserve(structuredClone(initialState), DEMO_MEMBER, slot);
    expect(() => reserve(state, DEMO_MEMBER, slot)).toThrow("Ya tienes");
  });
  it("rechaza un bloque completo", () => {
    expect(() =>
      reserve(
        structuredClone(initialState),
        DEMO_MEMBER,
        slotsFor(slot.date)[2],
      ),
    ).toThrow("no tiene cupos");
  });
  it("rechaza usuarios deshabilitados", () => {
    expect(() =>
      reserve(structuredClone(initialState), "staff-1", slot),
    ).toThrow("no está habilitada");
  });
  it("cancela, libera cupo y permite volver a reservar", () => {
    const state = reserve(structuredClone(initialState), DEMO_MEMBER, slot);
    const cancelled = cancel(state, state.reservations[0].id);
    expect(remaining(slot, cancelled)).toBe(13);
    expect(reserve(cancelled, DEMO_MEMBER, slot).reservations).toHaveLength(2);
  });
  it("completa el recorrido de entrada y salida", () => {
    const state = reserve(structuredClone(initialState), DEMO_MEMBER, slot);
    const code = ticket(state.reservations[0].id);
    const entered = access(state, code, "entry");
    expect(entered.reservations[0].entry).toBeTruthy();
    expect(remaining(slot, entered)).toBe(12);
    const exited = access(entered, code, "exit");
    expect(exited.reservations[0].status).toBe("Completada");
    expect(exited.reservations[0].exit).toBeTruthy();
  });
  it("rechaza doble entrada y salida sin entrada", () => {
    const state = reserve(structuredClone(initialState), DEMO_MEMBER, slot);
    const code = ticket(state.reservations[0].id);
    expect(() => access(state, code, "exit")).toThrow("No hay una entrada");
    expect(() => access(access(state, code, "entry"), code, "entry")).toThrow(
      "no está disponible",
    );
  });
  it("no permite cancelar después de ingresar", () => {
    const state = reserve(structuredClone(initialState), DEMO_MEMBER, slot);
    const entered = access(state, ticket(state.reservations[0].id), "entry");
    expect(() => cancel(entered, state.reservations[0].id)).toThrow(
      "Solo puedes",
    );
  });
  it("permite salir aunque se deshabilite la cuenta durante la visita", () => {
    let state = reserve(structuredClone(initialState), DEMO_MEMBER, slot);
    const code = ticket(state.reservations[0].id);
    state = access(state, code, "entry");
    state.members[0].enabled = false;
    expect(access(state, code, "exit").reservations[0].status).toBe(
      "Completada",
    );
  });
  it("rechaza códigos ajenos o desconocidos", () => {
    expect(() => access(initialState, "untrusted", "entry")).toThrow(
      "no válido",
    );
    expect(() => access(initialState, "UCN-DEMO:unknown", "entry")).toThrow(
      "No encontramos",
    );
  });
  it("genera seis días de atención y omite domingo", () => {
    const days = getDays(new Date("2026-11-14T12:00:00"));
    expect(days).toHaveLength(6);
    expect(days[0]).toBe("2026-11-14");
    expect(days[1]).toBe("2026-11-16");
  });
  it("recupera un estado seguro ante datos locales dañados", () => {
    localStorage.setItem(STORAGE_KEY, "{broken");
    expect(loadDemo()).toEqual(initialState);
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ members: [], reservations: [] }),
    );
    expect(loadDemo()).toEqual(initialState);
  });
});

import { describe, expect, it } from "vitest";
import {
  access,
  cancel,
  initialState,
  isPresent,
  isValidRut,
  presentReservations,
  reconcileAttendance,
  remaining,
  reserve,
  reserveGroup,
  slotsFor,
  ticket,
  type DemoState,
} from "./domain";

const base = () => structuredClone(initialState);
const now = new Date("2026-11-18T08:30:00");
const slots = slotsFor("2026-11-18");
const at = (time: string) => new Date(`2026-11-18T${time}:00`);

describe("reservas y grupos", () => {
  it("los bloques tienen capacidad de 30", () =>
    expect(slots.every((slot) => slot.capacity === 30)).toBe(true));
  it("permite varias reservas diarias a un deportista", () => {
    let state = reserve(base(), "student-1", slots[0], now);
    state = reserve(state, "student-1", slots[1], now);
    expect(state.reservations).toHaveLength(2);
    expect(state.reservations[0].createdAt).toBe(now.toISOString());
    expect(state.reservations[0].branch).toBe("Fútbol");
  });
  it("limita al estudiante a una reserva diaria y libera la cuota al cancelar", () => {
    const state = reserve(base(), "student-2", slots[0], now);
    expect(() => reserve(state, "student-2", slots[1], now)).toThrow(
      "una sesión por día",
    );
    expect(
      reserve(
        cancel(state, state.reservations[0].id),
        "student-2",
        slots[1],
        now,
      ).reservations,
    ).toHaveLength(2);
  });
  it("aplica la cuota semanal solo cuando existe una explícita", () => {
    const state = base();
    state.members.find((member) => member.id === "student-2")!.weeklyLimit = 1;
    const reserved = reserve(state, "student-2", slots[0], now);
    expect(() =>
      reserve(reserved, "student-2", slotsFor("2026-11-19")[0], now),
    ).toThrow("límite semanal");
  });
  it("rechaza bloques finalizados y cuentas pendientes de activación", () => {
    expect(() => reserve(base(), "student-1", slots[0], at("10:00"))).toThrow(
      "ya terminó",
    );
    const state = base();
    state.members[0].accountStatus = "Pendiente de activación";
    expect(() => reserve(state, "student-1", slots[0], now)).toThrow(
      "pendiente de activación",
    );
  });
  it("solo admin puede crear grupos; 60 minutos es válido y 59 no", () => {
    expect(() =>
      reserveGroup(base(), "reception", "Fútbol", ["athlete-2"], slots[1], now),
    ).toThrow("Solo un administrador");
    expect(() =>
      reserveGroup(base(), "student", "Fútbol", ["athlete-2"], slots[1], now),
    ).toThrow("Solo un administrador");
    const group = reserveGroup(
      base(),
      "admin",
      "Fútbol",
      ["student-1", "athlete-2", "athlete-3"],
      slots[1],
      now,
    );
    expect(group.reservations).toHaveLength(3);
    expect(group.groups).toHaveLength(1);
    expect(remaining(slots[1], group)).toBe(15);
    expect(() =>
      reserveGroup(
        base(),
        "admin",
        "Fútbol",
        ["athlete-2"],
        slots[1],
        at("08:31"),
      ),
    ).toThrow("una hora");
  });
  it("los grupos no mezclan ramas y se confirman completos o no se modifican", () => {
    const state = base();
    const snapshot = JSON.stringify(state);
    expect(() =>
      reserveGroup(
        state,
        "admin",
        "Fútbol",
        ["athlete-2", "athlete-4"],
        slots[1],
        now,
      ),
    ).toThrow("misma rama");
    expect(JSON.stringify(state)).toBe(snapshot);
    const almostFull = { ...slots[1], occupied: 29 };
    expect(() =>
      reserveGroup(
        state,
        "admin",
        "Fútbol",
        ["athlete-2", "athlete-3"],
        almostFull,
        now,
      ),
    ).toThrow("todos los deportistas");
  });
  it("no duplica al participante repetido ni una reserva existente", () => {
    const group = reserveGroup(
      base(),
      "admin",
      "Fútbol",
      ["athlete-2", "athlete-2"],
      slots[1],
      now,
    );
    expect(group.reservations).toHaveLength(1);
    expect(() =>
      reserveGroup(group, "admin", "Fútbol", ["athlete-2"], slots[1], now),
    ).toThrow("Ya tienes");
  });
});
describe("identificación, ingreso y reingreso", () => {
  it("valida el dígito verificador del RUT", () => {
    expect(isValidRut("12.345.678-5")).toBe(true);
    expect(isValidRut("12.345.678-0")).toBe(false);
    expect(isValidRut("sin-rut")).toBe(false);
  });
  it("permite exactamente 15 minutos antes y no permite 16", () => {
    const state = reserve(base(), "student-1", slots[1], now);
    const code = ticket(state.reservations[0].id);
    expect(() =>
      access(state, code, "entry", at("09:14"), "reception", "TNE"),
    ).toThrow("15 minutos");
    expect(
      isPresent(
        access(state, code, "entry", at("09:15"), "reception", "TNE")
          .reservations[0],
      ),
    ).toBe(true);
  });
  it("acepta atrasos hasta el fin y rechaza el instante final", () => {
    const state = reserve(base(), "student-1", slots[0], now);
    const code = ticket(state.reservations[0].id);
    expect(
      isPresent(access(state, code, "entry", at("09:29")).reservations[0]),
    ).toBe(true);
    expect(() => access(state, code, "entry", at("09:30"))).toThrow(
      "ya terminó",
    );
  });
  it("registra varias visitas sin borrar entradas ni liberar cupo", () => {
    let state = reserve(base(), "student-1", slots[0], now);
    const code = ticket(state.reservations[0].id);
    state = access(
      state,
      code,
      "entry",
      now,
      "reception",
      "Credencial universitaria",
    );
    state = access(state, code, "exit", at("08:45"), "admin");
    expect(state.reservations[0].status).toBe("Fuera de sala");
    expect(remaining(slots[0], state)).toBe(22);
    state = access(state, code, "entry", at("09:00"), "reception", "Carnet");
    state = access(state, code, "exit", at("09:20"), "reception");
    expect(state.reservations[0].visits).toHaveLength(2);
    expect(state.reservations[0].visits![0].exitOperatorRole).toBe("admin");
    expect(state.reservations[0].entry).toBe(now.toISOString());
    expect(presentReservations(state)).toHaveLength(0);
    expect(reconcileAttendance(state, at("09:30")).reservations[0].status).toBe(
      "Completada",
    );
  });
  it("permite la salida después del fin o estando deshabilitado", () => {
    let state = reserve(base(), "student-1", slots[0], now);
    const code = ticket(state.reservations[0].id);
    state = access(state, code, "entry", now);
    state.members[0].enabled = false;
    state = reconcileAttendance(state, at("09:31"));
    expect(presentReservations(state)).toHaveLength(1);
    expect(
      access(state, code, "exit", at("09:40")).reservations[0].status,
    ).toBe("Completada");
  });
  it("impide ingresar al usuario, a un RUT ausente y a la persona 31", () => {
    const state = reserve(base(), "student-1", slots[0], now);
    const code = ticket(state.reservations[0].id);
    expect(() => access(state, code, "entry", now, "student")).toThrow(
      "permisos",
    );
    const noRut = structuredClone(state);
    noRut.members[0].rut = "";
    expect(() => access(noRut, code, "entry", now)).toThrow("RUT válido");
    const full: DemoState = {
      ...state,
      reservations: [
        ...state.reservations,
        ...Array.from({ length: 30 }, (_, index) => ({
          id: `occupied-${index}`,
          memberId: `occupant-${index}`,
          slot: slots[0],
          status: "En sala" as const,
        })),
      ],
    };
    expect(() => access(full, code, "entry", now)).toThrow("30 personas");
  });
});
describe("inasistencias", () => {
  it("crea una alerta una sola vez y nunca sanciona automáticamente", () => {
    const state = reserve(base(), "student-1", slots[0], now);
    const processed = reconcileAttendance(state, at("09:30"));
    expect(processed.reservations[0].status).toBe("No asistió");
    expect(processed.alerts).toHaveLength(1);
    expect(processed.members[0].enabled).toBe(true);
    expect(reconcileAttendance(processed, at("10:00")).alerts).toHaveLength(1);
  });
  it("no marca como ausente una cancelación ni una visita breve", () => {
    let state = reserve(base(), "student-1", slots[0], now);
    expect(
      reconcileAttendance(cancel(state, state.reservations[0].id), at("10:00"))
        .alerts,
    ).toHaveLength(0);
    const code = ticket(state.reservations[0].id);
    state = access(state, code, "entry", now);
    state = access(state, code, "exit", at("08:31"));
    expect(reconcileAttendance(state, at("10:00")).alerts).toHaveLength(0);
  });
});

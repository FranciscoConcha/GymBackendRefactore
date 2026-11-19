import { describe, expect, it } from "vitest";
import { dashboardMetrics } from "./metrics";
import {
  initialState,
  slotsFor,
  type DemoState,
  type Reservation,
} from "./domain";

const now = new Date("2026-11-12T12:00:00");
function reservation(
  id: string,
  date: string,
  status: Reservation["status"],
  extra: Partial<Reservation> = {},
): Reservation {
  return {
    id,
    memberId: "student-1",
    slot: slotsFor(date)[0],
    status,
    ...extra,
  };
}
function sample(reservations: Reservation[]): DemoState {
  return { ...structuredClone(initialState), reservations };
}

describe("métricas del administrador", () => {
  it("no inventa porcentajes cuando no hay registros", () => {
    const result = dashboardMetrics(initialState, 7, now);
    expect(result.attendanceRate).toBeNull();
    expect(result.cancellationRate).toBeNull();
    expect(result.averageMinutes).toBeNull();
    expect(result.daily).toHaveLength(7);
    expect(result.records).toHaveLength(0);
  });
  it("filtra por fecha de sesión y excluye cancelaciones del denominador de asistencia", () => {
    const result = dashboardMetrics(
      sample([
        reservation("1", "2026-11-12", "Completada"),
        reservation("2", "2026-11-12", "Reservada"),
        reservation("3", "2026-11-12", "Cancelada"),
        reservation("4", "2026-11-01", "Reservada"),
        reservation("5", "2026-11-13", "Reservada"),
      ]),
      1,
      now,
    );
    expect(result.records).toHaveLength(3);
    expect(result.attendanceRate).toBe(50);
    expect(result.cancellationRate).toBe(33);
    expect(result.pending).toBe(1);
    expect(result.demand[0].count).toBe(2);
  });
  it("calcula permanencia solo con visitas completadas y tiempos válidos", () => {
    const result = dashboardMetrics(
      sample([
        reservation("1", "2026-11-12", "Completada", {
          entry: "2026-11-12T08:00:00Z",
          exit: "2026-11-12T09:00:00Z",
        }),
        reservation("2", "2026-11-12", "Completada", {
          entry: "2026-11-12T09:00:00Z",
          exit: "2026-11-12T09:30:00Z",
        }),
        reservation("3", "2026-11-12", "En sala", {
          entry: "2026-11-12T10:00:00Z",
        }),
        reservation("4", "2026-11-12", "Completada", {
          entry: "invalid",
          exit: "invalid",
        }),
      ]),
      7,
      now,
    );
    expect(result.averageMinutes).toBe(45);
  });
  it("la presencia actual no depende del período seleccionado", () => {
    const state = sample([reservation("1", "2026-11-01", "En sala")]);
    const result = dashboardMetrics(state, 1, now);
    expect(result.records).toHaveLength(0);
    expect(result.present).toHaveLength(1);
  });
  it("el conteo diario coincide con el total y no modifica el estado", () => {
    const state = sample([
      reservation("1", "2026-11-10", "Reservada"),
      reservation("2", "2026-11-12", "Cancelada"),
    ]);
    const before = JSON.stringify(state);
    const result = dashboardMetrics(state, 7, now);
    expect(result.daily.reduce((sum, day) => sum + day.total, 0)).toBe(
      result.records.length,
    );
    expect(JSON.stringify(state)).toBe(before);
  });
});

import { expect, it } from "vitest";
import { dashboardMetrics } from "./metrics";
import type { AppData } from "./domain";
const state: AppData = { members: [], reservations: [], slots: [] };
it("no divide por cero", () => {
  const m = dashboardMetrics(state, 7);
  expect(m.attendanceRate).toBeNull();
  expect(m.cancellationRate).toBeNull();
  expect(m.demand).toEqual([]);
});
it("usa estados y fechas reales", () => {
  const slot = {
    id: 1,
    date: "2026-11-18",
    start: "08:00",
    end: "09:00",
    capacity: 7,
    occupied: 1,
    zone: "Sala",
  };
  const m = dashboardMetrics(
    {
      ...state,
      reservations: [
        { id: 1, memberId: 1, userName: "Ana", slot, status: "Asistió" },
        { id: 2, memberId: 1, userName: "Ana", slot, status: "Cancelado" },
        {
          id: 3,
          memberId: 1,
          userName: "Ana",
          slot: { ...slot, date: "2026-10-01" },
          status: "Reservado",
        },
      ],
    },
    1,
    new Date("2026-11-18T12:00:00"),
  );
  expect(m.records).toHaveLength(2);
  expect(m.attendanceRate).toBe(100);
  expect(m.cancellationRate).toBe(50);
  expect(m.demand).toEqual([{ start: "08:00", count: 1 }]);
});

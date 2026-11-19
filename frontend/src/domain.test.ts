import { expect, it } from "vitest";
import { durationMinutes, getDays, hasStarted, remaining } from "./domain";
it("omite domingos", () => {
  expect(getDays(new Date("2026-11-14T12:00:00"))[1]).toBe("2026-11-16");
});
it("calcula cupos sin valores negativos", () => {
  expect(
    remaining({
      id: 1,
      date: "2026-11-14",
      start: "08:00",
      end: "09:00",
      capacity: 0,
      occupied: 2,
      zone: "Sala",
    }),
  ).toBe(0);
});
const slot = {
  id: 1,
  date: "2026-11-18",
  start: "08:00",
  end: "09:30",
  capacity: 20,
  occupied: 0,
  zone: "Sala",
};
it("bloque iniciado según fecha y hora", () => {
  expect(hasStarted(slot, new Date("2026-11-18T07:59:00"))).toBe(false);
  expect(hasStarted(slot, new Date("2026-11-18T08:00:00"))).toBe(true);
  expect(hasStarted(slot, new Date("2026-11-17T23:00:00"))).toBe(false);
  expect(hasStarted(slot, new Date("2026-11-19T07:00:00"))).toBe(true);
});
it("duración del bloque en minutos", () => {
  expect(durationMinutes(slot)).toBe(90);
});

import { expect, it } from "vitest";
import {
  formatRut,
  isValidRut,
  localDate,
  hasStarted,
  remaining,
  slotFrom,
  qrValidity,
} from "./domain";
it("normaliza y valida el RUT", () => {
  expect(formatRut("123456785")).toBe("12.345.678-5");
  expect(isValidRut("12.345.678-5")).toBe(true);
  expect(isValidRut("12.345.678-0")).toBe(false);
});
it("no suma otra vez las reservas a reservedCount, que incluye los grupos", () => {
  const slot = slotFrom({
    id: 1,
    day: "Lunes",
    date: "2026-11-19",
    startTime: "08:00",
    endTime: "09:30",
    zone: "Sala",
    capacity: 30,
    reservedCount: 18,
    individualReservations: 8,
    groupSeats: 10,
  });
  expect(remaining(slot)).toBe(12);
  expect(slot.groupSeats).toBe(10);
  expect(hasStarted(slot, new Date("2026-11-19T10:59:00Z"))).toBe(false);
  expect(hasStarted(slot, new Date("2026-11-19T11:00:00Z"))).toBe(true);
});
it("usa la fecha de Santiago y no la del navegador", () => {
  expect(localDate(new Date("2026-11-19T01:00:00Z"))).toBe("2026-11-18");
});
it("informa los límites inclusivo y exclusivo de vigencia", () => {
  const from = "2026-11-19T10:45:00Z",
    until = "2026-11-19T12:30:00Z";
  expect(qrValidity(from, until, Date.parse(from) - 1)).toContain("todavía");
  expect(qrValidity(from, until, Date.parse(from))).toContain("vigente para");
  expect(qrValidity(from, until, Date.parse(until))).toContain("venció");
});

import { expect, it } from "vitest";
import { attendanceRate } from "./metrics";
it("no divide por cero sin reservas o cuando están todas canceladas", () => {
  expect(
    attendanceRate({
      reservations: { total: 0, attended: 0, cancelled: 0, noShow: 0 },
    }),
  ).toBeNull();
  expect(
    attendanceRate({
      reservations: { total: 3, attended: 0, cancelled: 3, noShow: 0 },
    }),
  ).toBeNull();
});
it("calcula la asistencia con las cantidades del servidor", () => {
  expect(
    attendanceRate({
      reservations: { total: 10, attended: 4, cancelled: 2, noShow: 4 },
    }),
  ).toBe(50);
});

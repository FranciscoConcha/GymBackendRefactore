import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { api, type QrResponse } from "./api";
import BlockQr from "./pages/BlockQr";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

it("pausa consultas al ocultarse, refresca al volver y limpia al desmontar", async () => {
  vi.useFakeTimers();
  let hidden = false;
  vi.spyOn(document, "hidden", "get").mockImplementation(() => hidden);
  const response: QrResponse = {
    purpose: "Entrada",
    token: "entry",
    validFrom: "2026-11-19T11:00:00Z",
    validUntil: "2026-11-19T12:30:00Z",
    user: {
      firstName: "Alex",
      lastName: "Martínez",
      fullName: "Alex Martínez",
      rut: "12345678-5",
      category: "Deportista",
      sportBranch: null,
    },
    session: {
      kind: "Grupo",
      groupBookingId: 5,
      reservationId: null,
      date: "2026-11-19",
      startTime: "08:00",
      endTime: "09:30",
    },
  };
  const qr = vi.spyOn(api, "qr").mockResolvedValue(response);
  const view = render(<BlockQr id={5} group />);
  await act(async () => {});
  expect(qr).toHaveBeenCalledWith(5, true);
  hidden = true;
  act(() => document.dispatchEvent(new Event("visibilitychange")));
  await act(async () => {
    await vi.advanceTimersByTimeAsync(15000);
  });
  expect(qr).toHaveBeenCalledTimes(1);
  qr.mockResolvedValue({ ...response, purpose: "Salida", token: "exit" });
  hidden = false;
  await act(async () => {
    document.dispatchEvent(new Event("visibilitychange"));
  });
  expect(
    screen.getByRole("heading", { name: "QR de salida" }),
  ).toBeInTheDocument();
  expect(qr).toHaveBeenCalledTimes(2);
  view.unmount();
  await act(async () => {
    await vi.advanceTimersByTimeAsync(15000);
  });
  document.dispatchEvent(new Event("visibilitychange"));
  expect(qr).toHaveBeenCalledTimes(2);
});

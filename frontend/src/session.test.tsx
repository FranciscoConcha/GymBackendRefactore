import { StrictMode } from "react";
import { afterEach, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { api, type UserResponse } from "./api";
import Session from "./Session";
import AccountPage from "./pages/AccountPage";
import AccessPanel from "./pages/AccessPanel";
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  window.history.replaceState({}, "", "/");
});
const user: UserResponse = {
  id: 1,
  rut: "12345678-5",
  firstName: "Alex",
  lastName: "Martínez",
  fullName: "Alex Martínez",
  email: "alex@example.invalid",
  username: "alex",
  role: "Usuario",
  category: "Deportista",
  sportBranch: { id: 1, name: "Fútbol" },
  enabled: true,
  status: "Activa",
  mustChangePassword: true,
  createdAt: "2026-11-19T09:00:00Z",
  updatedAt: "2026-11-19T09:00:00Z",
};
it("retira el token de la URL y lo conserva para activar bajo StrictMode", async () => {
  window.history.replaceState(
    {},
    "",
    "/activar-cuenta?token=secret-token&source=email#form",
  );
  const fetcher = vi
    .fn()
    .mockResolvedValue(new Response('{"message":"Cuenta activada."}'));
  vi.stubGlobal("fetch", fetcher);
  render(
    <StrictMode>
      <AccountPage mode="activate" onBack={() => {}} />
    </StrictMode>,
  );
  expect(window.location.search).toBe("?source=email");
  expect(window.location.hash).toBe("#form");
  fireEvent.change(screen.getByLabelText("Contraseña", { exact: true }), {
    target: { value: "password123" },
  });
  fireEvent.change(
    screen.getByLabelText("Repetir contraseña", { exact: true }),
    { target: { value: "password123" } },
  );
  fireEvent.click(screen.getByRole("button", { name: "Guardar contraseña" }));
  await screen.findByText("La contraseña se actualizó correctamente.");
  const [url, options] = fetcher.mock.calls[0];
  expect(url).toBe("/api/v1/auth/activate");
  expect(JSON.parse(options.body)).toEqual({
    token: "secret-token",
    newPassword: "password123",
  });
  expect(options.headers["X-Gym-Csrf"]).toBe("1");
});
it("impide renderizar un destino autenticado con contraseña temporal y usa la sesión renovada", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockImplementation((url: string) =>
      Promise.resolve(
        new Response(
          JSON.stringify(
            url.endsWith("/auth/me")
              ? user
              : {
                  message: "Actualizada",
                  user: { ...user, mustChangePassword: false },
                  expiresAt: "2026-11-20T09:00:00Z",
                },
          ),
        ),
      ),
    ),
  );
  render(
    <MemoryRouter initialEntries={["/admin/usuarios"]}>
      <Session>{(u) => <p>Portal autorizado {u.id}</p>}</Session>
    </MemoryRouter>,
  );
  await screen.findByRole("heading", { name: "Cambiar contraseña temporal" });
  expect(screen.queryByText(/Portal autorizado/)).not.toBeInTheDocument();
  fireEvent.change(screen.getByLabelText("Contraseña actual"), {
    target: { value: "temporary" },
  });
  fireEvent.change(screen.getByLabelText("Nueva contraseña", { exact: true }), {
    target: { value: "password123" },
  });
  fireEvent.change(
    screen.getByLabelText("Repetir contraseña", { exact: true }),
    { target: { value: "password123" } },
  );
  fireEvent.click(screen.getByRole("button", { name: "Guardar contraseña" }));
  await screen.findByText("Portal autorizado 1");
});
it("un 403 fuerza cambio y un 401 desmonta los datos autenticados", async () => {
  const fetcher = vi
    .fn()
    .mockResolvedValue(
      new Response(JSON.stringify({ ...user, mustChangePassword: false })),
    );
  vi.stubGlobal("fetch", fetcher);
  render(
    <MemoryRouter>
      <Session>{() => <p>Datos autenticados</p>}</Session>
    </MemoryRouter>,
  );
  await screen.findByText("Datos autenticados");
  fetcher.mockResolvedValueOnce(
    new Response(
      JSON.stringify({
        message: "Debe cambiar su contraseña temporal antes de continuar.",
      }),
      { status: 403 },
    ),
  );
  await expect(api.current()).rejects.toMatchObject({ status: 403 });
  await screen.findByRole("heading", { name: "Cambiar contraseña temporal" });
  expect(screen.queryByText("Datos autenticados")).not.toBeInTheDocument();
  fetcher.mockResolvedValueOnce(
    new Response('{"message":"Sin sesión"}', { status: 401 }),
  );
  await expect(api.current()).rejects.toMatchObject({ status: 401 });
  await screen.findByRole("button", { name: "Ingresar" });
});
it("validar muestra identidad pero solo registra entrada tras documento y confirmación", async () => {
  const fetcher = vi.fn().mockImplementation((url: string) =>
    Promise.resolve(
      new Response(
        JSON.stringify(
          url.endsWith("/access/current")
            ? { occupancy: { current: 0, max: 30 }, people: [] }
            : url.endsWith("/access/validate")
              ? {
                  user,
                  session: {
                    kind: "Reserva",
                    reservationId: 1,
                    date: "2026-11-19",
                    startTime: "08:00",
                    endTime: "09:30",
                  },
                  inside: false,
                  canEnter: true,
                  occupancy: { current: 0, max: 30 },
                }
              : { message: "Entrada registrada.", visit: { id: 1 } },
        ),
      ),
    ),
  );
  vi.stubGlobal("fetch", fetcher);
  render(<AccessPanel />);
  fireEvent.change(screen.getByLabelText("Código de la reserva"), {
    target: { value: "signed-token" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Validar código" }));
  await screen.findByRole("heading", { name: "Alex Martínez" });
  expect(fetcher.mock.calls.some(([url]) => url.endsWith("/entry"))).toBe(
    false,
  );
  expect(
    screen.getByRole("button", { name: "Registrar entrada" }),
  ).toBeDisabled();
  fireEvent.change(screen.getByLabelText("Documento presentado"), {
    target: { value: "TNE" },
  });
  fireEvent.click(screen.getByRole("checkbox"));
  fireEvent.click(screen.getByRole("button", { name: "Registrar entrada" }));
  await screen.findByText("Entrada registrada.");
  await waitFor(() =>
    expect(
      fetcher.mock.calls.filter(([url]) => url.endsWith("/entry")),
    ).toHaveLength(1),
  );
  const call = fetcher.mock.calls.find(([url]) => url.endsWith("/entry"))!;
  expect(JSON.parse(call[1].body)).toEqual({
    token: "signed-token",
    documentType: "TNE",
  });
  expect(call[1].headers["Idempotency-Key"]).toBeTruthy();
});

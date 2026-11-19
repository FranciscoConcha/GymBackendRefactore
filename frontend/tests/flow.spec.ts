import { expect, test } from "@playwright/test";
import { login, mockApi, navigate, reserve } from "./api-fixture";
test("sesión real, reserva, QR por bloque, cancelación y almacenamiento vacío", async ({
  page,
}) => {
  const mock = await mockApi(page);
  await login(page);
  await reserve(page);
  const reservation = mock.calls.find(
    (c) => c.path === "/reservations" && c.method === "POST",
  )!;
  expect(reservation.headers["idempotency-key"]).toBeTruthy();
  await page.getByText("Ver código QR", { exact: true }).click();
  await expect(page.locator(".ticket-code")).toHaveText("signed-R-1");
  expect(mock.calls.some((c) => c.path === "/me/reservations/1/qr")).toBe(true);
  await navigate(page, "Mi código QR");
  await expect(page.locator(".ticket-code")).toHaveText("signed-R-1");
  await expect(page.getByText(/Vigencia:/)).toBeVisible();
  await navigate(page, "Mis reservas");
  await page.getByRole("button", { name: "Cancelar", exact: true }).click();
  await page
    .getByRole("button", { name: "Cancelar reserva", exact: true })
    .click();
  await expect(page.locator(".reservation-row .badge")).toHaveText("Cancelado");
  await navigate(page, "Mis visitas");
  await expect(
    page.getByRole("heading", {
      name: "Historial de mis visitas",
      exact: true,
    }),
  ).toBeVisible();
  expect(
    await page.evaluate(() => ({
      local: Object.keys(localStorage),
      session: Object.keys(sessionStorage),
    })),
  ).toEqual({ local: [], session: [] });
  expect(mock.csrfFailures).toEqual([]);
  expect(
    mock.calls
      .filter((c) => ["POST", "PATCH", "DELETE", "PUT"].includes(c.method))
      .every((c) => c.headers["x-gym-csrf"] === "1"),
  ).toBe(true);
  expect(mock.unknown).toEqual([]);
});
test("el rol de sesión manda sobre URL y query", async ({ page }) => {
  const mock = await mockApi(page, { role: "Usuario" });
  await page.goto("/admin/usuarios?usuario=2");
  await expect(page).toHaveURL(/\/estudiante\/inicio$/);
  await expect(
    page.getByRole("heading", { name: "Hola, Alex." }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("navigation")
      .getByRole("link", { name: "Usuarios", exact: true }),
  ).toHaveCount(0);
  expect(mock.calls.some((c) => c.path === "/auth/users")).toBe(false);
});
test("restaura sesión al recargar y un 401 vuelve al login", async ({
  page,
}) => {
  const mock = await mockApi(page);
  await login(page);
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Hola, Alex." }),
  ).toBeVisible();
  mock.expire();
  await navigate(page, "Reservar");
  await expect(
    page.getByRole("button", { name: "Ingresar", exact: true }),
  ).toBeVisible();
});
test("contraseña temporal bloquea destinos hasta cambiarla", async ({
  page,
}) => {
  const mock = await mockApi(page, { mustChangePassword: true });
  await login(page);
  await expect(
    page.getByRole("heading", { name: "Cambiar contraseña temporal" }),
  ).toBeVisible();
  await page.goto("/admin/inicio");
  await expect(
    page.getByRole("heading", { name: "Cambiar contraseña temporal" }),
  ).toBeVisible();
  await page
    .getByLabel("Contraseña actual", { exact: true })
    .fill("password123");
  await page
    .getByLabel("Nueva contraseña", { exact: true })
    .fill("new-password123");
  await page
    .getByLabel("Repetir contraseña", { exact: true })
    .fill("new-password123");
  await page.getByRole("button", { name: "Guardar contraseña" }).click();
  await expect(page).toHaveURL(/\/estudiante\/inicio$/);
  expect(
    mock.calls.find((c) => c.path === "/auth/change-password")?.body,
  ).toEqual({ currentPassword: "password123", newPassword: "new-password123" });
});
test("un 403 de cambio obligatorio desde cualquier pantalla bloquea la sesión", async ({
  page,
}) => {
  const mock = await mockApi(page, { role: "Usuario" });
  await page.goto("/estudiante/inicio");
  await expect(
    page.getByRole("heading", { name: "Hola, Alex." }),
  ).toBeVisible();
  mock.failNext(
    "/schedules",
    403,
    "Debe cambiar su contraseña temporal antes de continuar.",
  );
  await navigate(page, "Reservar");
  await expect(
    page.getByRole("heading", { name: "Cambiar contraseña temporal" }),
  ).toBeVisible();
  await expect(page.getByRole("navigation")).toHaveCount(0);
});
test("activación lee token, lo elimina de URL y envía newPassword con CSRF", async ({
  page,
}) => {
  const mock = await mockApi(page);
  await page.goto("/activar-cuenta?token=activation-token&source=email");
  await expect(page).toHaveURL(/\/activar-cuenta\?source=email$/);
  await page.getByLabel("Contraseña", { exact: true }).fill("new-password123");
  await page
    .getByLabel("Repetir contraseña", { exact: true })
    .fill("new-password123");
  await page.getByRole("button", { name: "Guardar contraseña" }).click();
  await expect(
    page.getByText("La contraseña se actualizó correctamente."),
  ).toBeVisible();
  const call = mock.calls.find((c) => c.path === "/auth/activate")!;
  expect(call.body).toEqual({
    token: "activation-token",
    newPassword: "new-password123",
  });
  expect(call.headers["x-gym-csrf"]).toBe("1");
  expect(
    await page.evaluate(() => [localStorage.length, sessionStorage.length]),
  ).toEqual([0, 0]);
});
test("recuperación siempre devuelve el mensaje genérico", async ({ page }) => {
  const mock = await mockApi(page);
  mock.failNext("/auth/password-resets", 429, "Demasiados intentos");
  await page.goto("/recuperar-contrasena");
  await page.getByLabel("Correo registrado").fill("unknown@example.invalid");
  await page.getByRole("button", { name: "Enviar enlace" }).click();
  await expect(
    page.getByText(
      "Si el correo está registrado, recibirás un enlace para restablecer tu contraseña.",
    ),
  ).toBeVisible();
});
test("ventana de inasistencias confirma lectura al cerrar", async ({
  page,
}) => {
  const mock = await mockApi(page, { role: "Usuario", alerts: true });
  await page.goto("/estudiante/inicio");
  const dialog = page.getByRole("dialog", {
    name: "Tienes reservas sin asistencia",
  });
  await expect(dialog).toBeVisible();
  await dialog.getByRole("button", { name: "Cerrar", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  expect(
    mock.calls.find((c) => c.path === "/me/alerts/1/acknowledge")?.headers[
      "x-gym-csrf"
    ],
  ).toBe("1");
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Hola, Alex." }),
  ).toBeVisible();
  await expect(page.getByRole("dialog")).toHaveCount(0);
});
test("409 del servidor se muestra y reintentar conserva la clave del envío", async ({
  page,
}) => {
  const mock = await mockApi(page);
  await login(page);
  mock.failNext("/reservations", 409, "No quedan cupos en este bloque.");
  await navigate(page, "Reservar");
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page
    .getByRole("button", { name: "Confirmar reserva", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText(
    "No quedan cupos en este bloque.",
  );
  await page
    .getByRole("button", { name: "Confirmar reserva", exact: true })
    .click();
  await expect(page.locator(".reservation-row")).toHaveCount(1);
  const calls = mock.calls.filter(
    (c) => c.path === "/reservations" && c.method === "POST",
  );
  expect(calls).toHaveLength(2);
  expect(calls[0].headers["idempotency-key"]).toBe(
    calls[1].headers["idempotency-key"],
  );
});

test("Mi código QR incluye grupos de la rama aunque no haya reservas individuales", async ({
  page,
}) => {
  const mock = await mockApi(page, { role: "Usuario" });
  mock.groups.push({
    id: 5,
    sportBranch: { id: 1, name: "Fútbol" },
    scheduleSlotId: 1,
    date: mock.date,
    startTime: "08:00",
    endTime: "09:30",
    mode: "Cupos",
    seats: 4,
    usedSeats: 0,
    status: "Activa",
    createdBy: "Equipo UCN",
    createdAt: new Date().toISOString(),
  });
  await page.goto("/estudiante/qr");
  await expect(page.locator(".ticket-code")).toHaveText("signed-G-5");
  await expect(page.locator(".qr-person")).toContainText(mock.student.fullName);
  await expect(page.locator(".qr-person")).toContainText("12.345.678-5");
  expect(mock.calls.some((c) => c.path === "/me/group-bookings/5/qr")).toBe(
    true,
  );
});

test("QR abierto alterna entrada, salida y entrada con refresco automático", async ({
  page,
}) => {
  const mock = await mockApi(page);
  await login(page);
  await reserve(page);
  await page.clock.install();
  await navigate(page, "Mi código QR");
  await expect(
    page.getByRole("heading", { name: "QR de entrada", exact: true }),
  ).toBeVisible();
  const visit = {
    id: 1,
    userId: 1,
    fullName: mock.student.fullName,
    rut: mock.student.rut,
    reservationId: 1,
    groupBookingId: null,
    date: mock.date,
    entryAt: new Date().toISOString(),
    exitAt: null as string | null,
    stayMinutes: null,
    documentType: "TNE" as const,
    entryOperator: "Equipo UCN",
    exitOperator: null,
  };
  mock.visits.push(visit);
  await page.clock.runFor(5000);
  await expect(
    page.getByRole("heading", { name: "QR de salida", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".ticket-code")).toHaveText("signed-S-1");
  await expect(page.getByText("Muéstralo al salir")).toBeVisible();
  await expect(page.locator(".qr-frame")).toHaveClass(/qr-exit/);
  visit.exitAt = new Date().toISOString();
  await page.clock.runFor(5000);
  await expect(
    page.getByRole("heading", { name: "QR de entrada", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".ticket-code")).toHaveText("signed-R-1");
  await navigate(page, "Mis visitas");
  const queries = mock.calls.filter((c) => c.path.endsWith("/qr")).length;
  await page.clock.runFor(10000);
  expect(mock.calls.filter((c) => c.path.endsWith("/qr"))).toHaveLength(
    queries,
  );
});

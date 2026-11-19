import { expect, test } from "@playwright/test";
import { login, mockApi, navigate } from "./api-fixture";
test("acceso en dos pasos; salida por persona sin reutilizar QR", async ({
  page,
}) => {
  const mock = await mockApi(page);
  await login(page, "Recepcionista");
  await expect(page).toHaveURL(/\/recepcion\/inicio$/);
  await expect(page.locator(".sidebar .nav-link")).toHaveCount(2);
  await navigate(page, "Escanear QR");
  await page.getByLabel("Código de la reserva").fill("signed-R-1");
  await page.getByRole("button", { name: "Validar código" }).click();
  await expect(
    page.getByRole("heading", { name: "Alex Martínez" }),
  ).toBeVisible();
  expect(mock.visits).toHaveLength(0);
  expect(mock.calls.some((c) => c.path === "/access/entry")).toBe(false);
  await expect(
    page.getByRole("button", { name: "Registrar entrada" }),
  ).toBeDisabled();
  await page
    .getByLabel("Documento presentado", { exact: true })
    .selectOption("TNE");
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Registrar entrada" }).click();
  await expect(
    page.getByText("Entrada registrada.", { exact: true }),
  ).toBeVisible();
  expect(mock.visits).toHaveLength(1);
  const entry = mock.calls.find((c) => c.path === "/access/entry")!;
  expect(entry.body).toEqual({ token: "signed-R-1", documentType: "TNE" });
  expect(entry.headers["idempotency-key"]).toBeTruthy();
  await page
    .getByRole("button", { name: "Salida manual (sin QR)", exact: true })
    .click();
  await page.getByLabel("Persona dentro").selectOption("1");
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Registrar salida" }).click();
  await expect(
    page.getByText("Salida registrada.", { exact: true }),
  ).toBeVisible();
  expect(mock.calls.find((c) => c.path === "/access/exit")?.body).toEqual({
    userId: 1,
  });
  expect(mock.visits[0].exitAt).toBeTruthy();
  expect(mock.csrfFailures).toEqual([]);
  expect(mock.unknown).toEqual([]);
});
test("grupo no nominativo por cupos y clave conservada al reintentar", async ({
  page,
}) => {
  const mock = await mockApi(page, { role: "Admin" });
  await page.goto("/admin/grupos");
  await page.getByLabel("Rama deportiva", { exact: true }).selectOption("1");
  await page.getByLabel("Horario", { exact: true }).selectOption("1");
  await page.getByLabel("Cantidad de cupos").fill("4");
  mock.failNext("/group-bookings", 503, "Intenta nuevamente.");
  await page.getByRole("button", { name: "Confirmar sesión de rama" }).click();
  await expect(page.getByRole("alert")).toHaveText("Intenta nuevamente.");
  await page.getByRole("button", { name: "Confirmar sesión de rama" }).click();
  await expect(page.getByText("4 cupos · 0 usados · Activa")).toBeVisible();
  const calls = mock.calls.filter(
    (c) => c.path === "/group-bookings" && c.method === "POST",
  );
  expect(calls).toHaveLength(2);
  expect(calls[0].body).toEqual({
    sportBranchId: 1,
    scheduleSlotId: 1,
    date: mock.date,
    mode: "Cupos",
    seats: 4,
  });
  expect(calls[0].headers["idempotency-key"]).toBeTruthy();
  expect(calls[0].headers["idempotency-key"]).toBe(
    calls[1].headers["idempotency-key"],
  );
  await page.getByRole("button", { name: "Cancelar grupo" }).click();
  await expect(page.getByText("4 cupos · 0 usados · Cancelada")).toBeVisible();
});
test("grupo usa todo el cupo disponible sin mandar seats", async ({ page }) => {
  const mock = await mockApi(page, { role: "Admin" });
  await page.goto("/admin/grupos");
  await page.getByLabel("Rama deportiva", { exact: true }).selectOption("1");
  await page.getByLabel("Horario", { exact: true }).selectOption("1");
  await page
    .getByLabel("Modo", { exact: true })
    .selectOption("TotalDisponible");
  await page.getByRole("button", { name: "Confirmar sesión de rama" }).click();
  await expect(page.getByText("30 cupos · 0 usados · Activa")).toBeVisible();
  expect(
    mock.calls.find((c) => c.path === "/group-bookings" && c.method === "POST")
      ?.body,
  ).not.toHaveProperty("seats");
});
test("Excel sube multipart, muestra preview y confirma con clave estable", async ({
  page,
}) => {
  const mock = await mockApi(page, { role: "Admin" });
  await page.goto("/admin/archivos");
  await page.getByLabel("Archivo de usuarios (.xlsx)").setInputFiles({
    name: "usuarios.xlsx",
    mimeType:
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    buffer: Buffer.from("fixture-upload"),
  });
  await expect(
    page.getByRole("heading", { name: "Vista previa" }),
  ).toBeVisible();
  await expect(
    page.getByText("1 para crear · 1 omitidas · 1 con errores"),
  ).toBeVisible();
  await expect(
    page.getByRole("cell", { name: "Crear", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("RUT inválido.", { exact: true })).toBeVisible();
  expect(mock.calls.some((c) => c.path.endsWith("/confirm"))).toBe(false);
  mock.failNext("/admin/user-imports/1/confirm", 503, "Intenta nuevamente.");
  await page.getByRole("button", { name: "Confirmar importación (1)" }).click();
  await expect(page.getByRole("alert")).toHaveText("Intenta nuevamente.");
  await page.getByRole("button", { name: "Confirmar importación (1)" }).click();
  await expect(
    page.getByText("1 creados · 1 omitidos · 1 fallidos"),
  ).toBeVisible();
  const calls = mock.calls.filter(
    (c) => c.path === "/admin/user-imports/1/confirm",
  );
  expect(calls).toHaveLength(2);
  expect(calls[0].headers["idempotency-key"]).toBeTruthy();
  expect(calls[0].headers["idempotency-key"]).toBe(
    calls[1].headers["idempotency-key"],
  );
  expect(mock.csrfFailures).toEqual([]);
});
test("exporta con filtros y nombre Content-Disposition", async ({ page }) => {
  const mock = await mockApi(page, { role: "Admin" });
  await page.goto("/admin/archivos");
  await page.getByLabel("Desde", { exact: true }).fill("2026-11-01");
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Exportar todo (.xlsx)" }).click();
  expect((await download).suggestedFilename()).toBe("UCN-reporte.xlsx");
  expect(mock.calls.find((c) => c.path === "/admin/exports")?.body).toEqual({
    from: "2026-11-01",
  });
});
test("usuarios y ramas se administran mediante mutaciones CSRF", async ({
  page,
}) => {
  const mock = await mockApi(page, { role: "Admin" });
  await page.goto("/admin/ramas");
  await expect(
    page.getByRole("button", { name: "Crear rama", exact: true }),
  ).toHaveCount(0);
  await expect(page.getByRole("textbox")).toHaveCount(0);
  await page.getByRole("button", { name: "Deshabilitar rama" }).first().click();
  await expect(
    page.getByRole("button", { name: "Habilitar rama", exact: true }),
  ).toBeVisible();
  expect(
    mock.calls.find((c) => c.path === "/branches/1" && c.method === "PATCH")
      ?.body,
  ).toEqual({ enabled: false });
  expect(
    mock.calls.some((c) => c.path === "/branches" && c.method === "POST"),
  ).toBe(false);
  await navigate(page, "Usuarios");
  await page
    .getByRole("button", { name: "Agregar usuario", exact: true })
    .click();
  const dialog = page.getByRole("dialog", { name: "Agregar usuario" });
  await dialog.getByLabel("Nombre", { exact: true }).fill("Camila");
  await dialog.getByLabel("Apellido", { exact: true }).fill("Rojas");
  await dialog.getByLabel("RUT", { exact: true }).fill("11.111.111-1");
  await dialog
    .getByLabel("Correo", { exact: true })
    .fill("camila@example.invalid");
  await dialog
    .getByLabel("Categoría", { exact: true })
    .selectOption("Estudiante");
  await dialog
    .getByRole("button", { name: "Agregar usuario", exact: true })
    .click();
  await expect(dialog).toHaveCount(0);
  expect(
    mock.calls.find((c) => c.path === "/auth/register")?.body,
  ).not.toHaveProperty("password");
  await page
    .getByRole("button", { name: "Agregar usuario", exact: true })
    .click();
  const adminDialog = page.getByRole("dialog", { name: "Agregar usuario" });
  await adminDialog.getByLabel("Rol", { exact: true }).selectOption("Admin");
  await expect(
    adminDialog.getByLabel("Contraseña temporal (opcional)"),
  ).toHaveCount(0);
  await expect(
    adminDialog.getByText("recibirá una invitación por correo"),
  ).toBeVisible();
  await adminDialog
    .getByLabel("Nombre de usuario", { exact: true })
    .fill("nuevo.admin");
  await adminDialog.getByLabel("Nombre", { exact: true }).fill("Ana");
  await adminDialog.getByLabel("Apellido", { exact: true }).fill("Soto");
  await adminDialog.getByLabel("RUT", { exact: true }).fill("22.222.222-2");
  await adminDialog
    .getByLabel("Correo", { exact: true })
    .fill("ana@example.invalid");
  await adminDialog
    .getByRole("button", { name: "Agregar usuario", exact: true })
    .click();
  await expect(adminDialog).toHaveCount(0);
  const adminBody = mock.calls.filter((c) => c.path === "/auth/register")[1]
    ?.body;
  expect(adminBody).toMatchObject({ role: "Admin", username: "nuevo.admin" });
  expect(adminBody).not.toHaveProperty("password");
  expect(mock.csrfFailures).toEqual([]);
});

test("operador valida salida sin documento, conserva clave al reintentar y rechaza reutilización", async ({
  page,
}) => {
  const mock = await mockApi(page, { role: "Recepcionista" });
  const visit = {
    id: 1,
    userId: 1,
    fullName: mock.student.fullName,
    rut: mock.student.rut,
    reservationId: 1,
    groupBookingId: null,
    date: mock.date,
    entryAt: new Date().toISOString(),
    exitAt: null,
    stayMinutes: null,
    documentType: "TNE" as const,
    entryOperator: "Equipo UCN",
    exitOperator: null,
  };
  mock.visits.push(visit);
  await page.goto("/recepcion/acceso");
  await page.getByLabel("Código de la reserva").fill("signed-S-1");
  await page.getByRole("button", { name: "Validar código" }).click();
  await expect(
    page.getByRole("heading", { name: "Alex Martínez" }),
  ).toBeVisible();
  await expect(
    page.getByLabel("Documento presentado", { exact: true }),
  ).toHaveCount(0);
  expect(mock.visits[0].exitAt).toBeNull();
  mock.failNext("/access/exit", 503, "Reintente la salida.");
  await page
    .getByRole("button", { name: "Registrar salida", exact: true })
    .click();
  await expect(page.getByRole("alert")).toHaveText("Reintente la salida.");
  await page
    .getByRole("button", { name: "Registrar salida", exact: true })
    .click();
  await expect(
    page.getByText("Salida registrada.", { exact: true }),
  ).toBeVisible();
  const calls = mock.calls.filter((c) => c.path === "/access/exit");
  expect(calls).toHaveLength(2);
  expect(calls[1].body).toEqual({ token: "signed-S-1" });
  expect(calls[1].headers["x-gym-csrf"]).toBe("1");
  expect(calls[0].headers["idempotency-key"]).toBeTruthy();
  expect(calls[1].headers["idempotency-key"]).toBe(
    calls[0].headers["idempotency-key"],
  );
  await page.getByLabel("Código de la reserva").fill("signed-S-1");
  await page.getByRole("button", { name: "Validar código" }).click();
  await expect(
    page.getByText("Este QR de salida ya fue utilizado."),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Registrar salida", exact: true }),
  ).toBeDisabled();
  expect(
    await page.evaluate(() => [localStorage.length, sessionStorage.length]),
  ).toEqual([0, 0]);
  expect(mock.csrfFailures).toEqual([]);
  expect(mock.unknown).toEqual([]);
});

test("Estado actual permite cancelar y confirmar salida manual por persona", async ({
  page,
}) => {
  const mock = await mockApi(page, { role: "Recepcionista" });
  mock.visits.push({
    id: 1,
    userId: 1,
    fullName: mock.student.fullName,
    rut: mock.student.rut,
    reservationId: 1,
    groupBookingId: null,
    date: mock.date,
    entryAt: new Date().toISOString(),
    exitAt: null,
    stayMinutes: null,
    documentType: "TNE",
    entryOperator: "Equipo UCN",
    exitOperator: null,
  });
  await page.goto("/recepcion/inicio");
  await expect(
    page.getByText("Salida manual (sin QR)", { exact: true }),
  ).toBeVisible();
  page.once("dialog", (dialog) => dialog.dismiss());
  await page.getByRole("button", { name: "Marcar salida" }).click();
  expect(mock.calls.some((c) => c.path === "/access/exit")).toBe(false);
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Marcar salida" }).click();
  await expect(
    page.getByRole("heading", { name: "La sala está libre" }),
  ).toBeVisible();
  const call = mock.calls.find((c) => c.path === "/access/exit")!;
  expect(call.body).toEqual({ userId: 1 });
  expect(call.headers["x-gym-csrf"]).toBe("1");
  expect(call.headers["idempotency-key"]).toBeTruthy();
});

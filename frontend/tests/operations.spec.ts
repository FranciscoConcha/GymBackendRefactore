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
  await page.getByRole("button", { name: "Salida", exact: true }).click();
  await page.getByLabel("Persona dentro").selectOption("1");
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
  await page.getByLabel("Nombre de la rama", { exact: true }).fill("Natación");
  await page.getByRole("button", { name: "Crear rama", exact: true }).click();
  await expect(
    page.getByLabel("Nombre de Natación", { exact: true }),
  ).toBeVisible();
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
  expect(mock.csrfFailures).toEqual([]);
});

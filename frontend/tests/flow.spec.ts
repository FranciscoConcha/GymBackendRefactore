import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { login, mockApi, navigate, reserve } from "./api-fixture";

test("login fallido muestra el mensaje del servidor", async ({ page }) => {
  await mockApi(page);
  await page.goto("/");
  await page.getByLabel("RUT", { exact: true }).fill("123");
  await page.getByLabel("Contraseña", { exact: true }).fill("incorrecta");
  await page.getByRole("button", { name: "Ingresar" }).click();
  await expect(page.getByRole("alert")).toHaveText("Credenciales incorrectas");
  await expect(page.getByLabel("Contraseña", { exact: true })).toHaveValue("");
});

test("restaura sesión y corrige URL ajena al rol", async ({ page }) => {
  const api = await mockApi(page);
  api.restore("student");
  await page.goto("/admin/inicio");
  await expect(page).toHaveURL(/\/estudiante\/inicio$/);
  await expect(
    page.getByRole("heading", { name: "Hola, Alex." }),
  ).toBeVisible();
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Hola, Alex." }),
  ).toBeVisible();
});

test("401 durante sesión vuelve al login con aviso", async ({ page }) => {
  const api = await mockApi(page);
  await login(page);
  await page.route("**/api/v1/qr/my-code", async (route) => {
    api.expire();
    await route.fulfill({ status: 401, json: { message: "Sin sesión" } });
  });
  await navigate(page, "Mi código QR");
  await expect(page.getByRole("button", { name: "Ingresar" })).toBeVisible();
  await expect(page.getByRole("alert")).toContainText(/sesión|Sin sesión/);
});

test("reserva, QR personal, entrada y salida con CSRF y storage vacío", async ({
  page,
}) => {
  const api = await mockApi(page);
  await login(page);
  await reserve(page);
  await navigate(page, "Mi código QR");
  await expect(page.locator(".qr-frame svg")).toBeVisible();
  const token = await page.locator(".ticket-code").innerText();
  api.restore("admin");
  await page.goto("/admin/acceso");
  await page.getByLabel("Código personal del día").fill(token);
  await page
    .getByRole("button", { name: "Registrar entrada", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Entrada registrada", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Salida", exact: true }).click();
  await page
    .getByRole("button", { name: "Registrar salida", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Salida registrada", exact: true }),
  ).toBeVisible();
  api.restore("student");
  await page.goto("/estudiante/reservas");
  await expect(page.locator(".reservation-row .badge")).toHaveText("Asistió");
  await page.reload();
  await expect(page.locator(".reservation-row .badge")).toHaveText("Asistió");
  expect(api.mutations).toContain("POST /attendance/entry");
  expect(api.mutations).toContain("POST /attendance/exit");
  expect(
    await page.evaluate(() => [localStorage.length, sessionStorage.length]),
  ).toEqual([0, 0]);
});

test("cancelación confirmada, cupo completo y Escape", async ({ page }) => {
  await mockApi(page);
  await login(page);
  await page.getByRole("button", { name: "Reservar un horario" }).click();
  await expect(
    page.getByRole("button", { name: "Bloque completo" }),
  ).toBeDisabled();
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page
    .getByRole("button", { name: "Confirmar reserva", exact: true })
    .click();
  await page.getByRole("button", { name: "Cancelar", exact: true }).click();
  await page
    .getByRole("button", { name: "Cancelar reserva", exact: true })
    .click();
  await expect(page.locator(".reservation-row .badge")).toHaveText("Cancelado");
});

test("409 conserva el diálogo y muestra el error", async ({ page }) => {
  const api = await mockApi(page);
  await login(page);
  api.conflict();
  await page.getByRole("button", { name: "Reservar un horario" }).click();
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page
    .getByRole("button", { name: "Confirmar reserva", exact: true })
    .click();
  await expect(page.getByRole("alert")).toContainText("No quedan cupos");
  await expect(page.getByRole("dialog")).toBeVisible();
});

test("usuarios: registro, búsqueda y estado", async ({ page }) => {
  await mockApi(page);
  await login(page, true);
  await navigate(page, "Usuarios");
  await page
    .getByRole("button", { name: "Agregar usuario", exact: true })
    .click();
  await page.getByLabel("RUT", { exact: true }).fill("11111111-1");
  await page.getByLabel("Contraseña inicial").fill("temporal");
  await page.getByLabel("Nombre", { exact: true }).fill("Persona Prueba");
  await page
    .getByLabel("Correo", { exact: true })
    .fill("persona@example.invalid");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Agregar usuario", exact: true })
    .click();
  await page
    .getByRole("textbox", { name: "Buscar usuarios" })
    .fill("Persona Prueba");
  await expect(page.locator("tbody tr")).toHaveCount(1);
  const toggle = page.getByRole("switch", {
    name: "Habilitar acceso de Persona Prueba",
  });
  await toggle.click();
  await expect(toggle).toHaveAttribute("aria-checked", "false");
});

test("día elegido, salto al contenido, accesibilidad y ancho", async ({
  page,
}) => {
  await mockApi(page);
  await page.goto("/");
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await login(page);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  const day = await page
    .locator(".home-day")
    .nth(3)
    .locator("strong")
    .innerText();
  await page.locator(".home-day").nth(3).click();
  await expect(
    page.locator('.date-option[aria-pressed="true"] strong'),
  ).toHaveText(day);
  await page.locator(".skip-link").focus();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/estudiante\/horarios$/);
  await expect(page.locator("#main-content")).toBeFocused();
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});

test("logout invalida sesión restaurable", async ({ page }) => {
  const api = await mockApi(page);
  await login(page);
  const menu = page.getByRole("button", { name: "Abrir menú" });
  if (await menu.isVisible()) await menu.click();
  await page.locator(".logout").click();
  await expect(page.getByRole("button", { name: "Ingresar" })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("button", { name: "Ingresar" })).toBeVisible();
  expect(api.mutations).toContain("POST /auth/logout");
});

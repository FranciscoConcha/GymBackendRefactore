import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";

test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(new Date("2026-11-18T08:30:00"));
});

async function navigate(page: Page, label: string) {
  await expect(page.locator(".topbar")).toBeVisible();
  const menu = page.getByRole("button", { name: "Abrir menú" });
  if (await menu.isVisible()) await menu.click();
  await page
    .getByRole("navigation")
    .getByRole("link", { name: label, exact: true })
    .click();
}

test("portales independientes, navegación propia y panel administrativo accesible", async ({
  page,
}) => {
  await page.goto("/admin/inicio");
  await expect(
    page.getByRole("heading", { name: "Panel general", exact: true }),
  ).toBeVisible();
  await expect(page.locator(".metric-card")).toHaveCount(4);
  await expect(page.locator(".activity-echart svg")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Ver estudiante" }),
  ).toHaveCount(0);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await page.getByRole("button", { name: "30 días", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "30 días", exact: true }),
  ).toHaveAttribute("aria-pressed", "true");
  await expect(page.locator("body")).not.toContainText(
    /demostración|datos ficticios|sin conexión|modo demo/i,
  );
  await page.goto("/estudiante/inicio");
  await expect(
    page.getByRole("heading", { name: "Hola, Alex." }),
  ).toBeVisible();
  await expect(page.locator(".metric-card")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Ver recepción" })).toHaveCount(
    0,
  );
  await page.reload();
  await expect(
    page.getByRole("heading", { name: "Hola, Alex." }),
  ).toBeVisible();
});

test("el día elegido en inicio se conserva al abrir horarios", async ({
  page,
}) => {
  await page.goto("/");
  await page
    .getByRole("button", {
      name: "Usuario / Estudiante Reservas y código de acceso",
    })
    .click();
  const day = await page
    .locator(".home-day")
    .nth(3)
    .locator("strong")
    .textContent();
  await page.locator(".home-day").nth(3).click();
  await expect(
    page.locator('.date-option[aria-pressed="true"] strong'),
  ).toHaveText(day!);
});

test("accesibilidad automática en acceso, inicio y agenda", async ({
  page,
}) => {
  await page.goto("/");
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await page
    .getByRole("button", {
      name: "Usuario / Estudiante Reservas y código de acceso",
    })
    .click();
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await page.getByRole("button", { name: "Reservar un horario" }).click();
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
});

test("el enlace de salto conserva la pantalla actual", async ({ page }) => {
  await page.goto("/");
  await page
    .getByRole("button", {
      name: "Usuario / Estudiante Reservas y código de acceso",
    })
    .click();
  await page.getByRole("button", { name: "Reservar un horario" }).click();
  await expect(
    page.getByRole("heading", { name: "Encuentra tu horario." }),
  ).toBeVisible();
  await page.locator(".skip-link").focus();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/estudiante\/horarios$/);
  await expect(page.locator("#main-content")).toBeFocused();
});

test("reserva → QR → entrada → salida → historial", async ({ page }) => {
  await page.goto("/");
  await page
    .getByRole("button", {
      name: "Usuario / Estudiante Reservas y código de acceso",
    })
    .click();
  await page.getByRole("button", { name: "Reservar un horario" }).click();
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page.getByRole("button", { name: "Confirmar reserva" }).click();
  await expect(
    page.getByRole("heading", { name: "Mis reservas" }),
  ).toBeVisible();
  await expect(page.locator(".reservation-row")).toHaveCount(1);
  await navigate(page, "Mi código QR");
  await expect(page.locator(".qr-frame svg")).toBeVisible();
  const code = await page.locator(".ticket-code").textContent();
  await page.goto("/admin/acceso");
  await page.getByLabel("Código de la reserva").fill(code!);
  await page
    .getByRole("checkbox", { name: /Confirmo que el nombre y RUT/ })
    .check();
  await page
    .getByRole("button", { name: "Registrar entrada", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Entrada registrada" }),
  ).toBeVisible();
  await expect(page.locator(".present-member")).toHaveCount(1);
  await page.getByRole("button", { name: "Salida", exact: true }).click();
  await page
    .getByRole("button", { name: "Registrar salida", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Salida registrada" }),
  ).toBeVisible();
  await expect(page.locator(".present-member")).toHaveCount(0);
  await page.goto("/estudiante/reservas");
  await expect(page.locator(".reservation-row .badge")).toHaveText(
    "Fuera de sala",
  );
  await expect(page.locator(".reservation-row")).toContainText("Entrada");
  await expect(page.locator(".reservation-row")).toContainText("Salida");
  await page.clock.setFixedTime(new Date("2026-11-18T10:00:00"));
  await page.reload();
  await expect(page.locator(".reservation-row .badge")).toHaveText(
    "Completada",
  );
});

test("cancelación con confirmación y cupo completo deshabilitado", async ({
  page,
}) => {
  await page.goto("/");
  await page
    .getByRole("button", {
      name: "Usuario / Estudiante Reservas y código de acceso",
    })
    .click();
  await page.getByRole("button", { name: "Reservar un horario" }).click();
  await expect(
    page.getByRole("button", { name: "Bloque completo" }),
  ).toBeDisabled();
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page.getByRole("button", { name: "Volver", exact: true }).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page.getByRole("button", { name: "Confirmar reserva" }).click();
  await page.getByRole("button", { name: "Cancelar", exact: true }).click();
  await page
    .getByRole("button", { name: "Cancelar reserva", exact: true })
    .click();
  await expect(page.locator(".reservation-row .badge")).toHaveText("Cancelada");
});

test("gestiona usuarios y conserva la búsqueda", async ({ page }) => {
  await page.goto("/");
  await page
    .getByRole("button", { name: "Administrador Usuarios y control de acceso" })
    .click();
  await navigate(page, "Usuarios");
  await page
    .getByRole("button", { name: "Agregar usuario", exact: true })
    .click();
  await page
    .getByLabel("Nombre y apellido", { exact: true })
    .fill("Persona Demo");
  await page.getByLabel("RUT", { exact: true }).fill("22.222.222-2");
  await page.getByLabel("Rama deportiva", { exact: true }).fill("Fútbol");
  await page
    .getByLabel("Correo", { exact: true })
    .fill("persona@example.invalid");
  await page
    .getByRole("dialog")
    .getByRole("button", { name: "Agregar usuario", exact: true })
    .click();
  await page
    .getByRole("textbox", { name: "Buscar usuarios" })
    .fill("Persona Demo");
  await expect(page.locator("tbody tr")).toHaveCount(1);
  const toggle = page.getByRole("switch", {
    name: "Habilitar acceso de Persona Demo",
  });
  await toggle.click();
  await expect(toggle).toHaveAttribute("aria-checked", "false");
  await expect(page.locator("tbody")).toContainText("Deshabilitado");
});

test("sin desborde horizontal y diálogo cierra con Escape", async ({
  page,
}) => {
  await page.goto("/");
  await page
    .getByRole("button", {
      name: "Usuario / Estudiante Reservas y código de acceso",
    })
    .click();
  expect(
    await page.evaluate(
      () =>
        document.documentElement.scrollWidth <=
        document.documentElement.clientWidth,
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "Reservar un horario" }).click();
  expect(
    await page.evaluate(
      () =>
        document.documentElement.scrollWidth <=
        document.documentElement.clientWidth,
    ),
  ).toBe(true);
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toHaveCount(0);
});

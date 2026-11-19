import { expect, test, type Page } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import writeXlsxFile from "write-excel-file/node";
import { readSheet } from "read-excel-file/node";
import {
  initialState,
  reserve,
  slotsFor,
  STORAGE_KEY,
  ticket,
  type DemoState,
} from "../src/domain";
import { IMPORT_HEADERS } from "../src/excel";

const now = new Date("2026-11-18T08:30:00");
test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(now);
});
async function seed(page: Page, value: DemoState) {
  await page.addInitScript(
    ({ key, value }) => {
      if (!localStorage.getItem(key))
        localStorage.setItem(key, JSON.stringify(value));
    },
    { key: STORAGE_KEY, value },
  );
}

test("recepción: solo estado actual, identidad y accesos con reingreso", async ({
  page,
}) => {
  const state = reserve(
    structuredClone(initialState),
    "student-1",
    slotsFor("2026-11-18")[0],
    now,
  );
  await seed(page, state);
  await page.goto("/recepcion/usuarios");
  await expect(
    page.getByRole("heading", { name: "Estado actual de la sala" }),
  ).toBeVisible();
  await expect(page.getByRole("table")).toHaveCount(0);
  await expect(page.locator(".metric-card")).toHaveCount(0);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  await page.getByRole("button", { name: "Escanear QR", exact: true }).click();
  await expect(page.locator(".reservation-lookup")).toHaveCount(0);
  await page
    .getByLabel("Código de la reserva")
    .fill(ticket(state.reservations[0].id));
  await expect(page.locator(".identity-review")).toContainText("Alex Martínez");
  await expect(page.locator(".identity-review")).toContainText("12.345.678-5");
  const confirm = page.getByRole("button", {
    name: "Registrar entrada",
    exact: true,
  });
  await expect(confirm).toBeDisabled();
  await page
    .getByRole("combobox", { name: "Documento presentado", exact: true })
    .selectOption("TNE");
  await page
    .getByRole("checkbox", { name: /Confirmo que el nombre y RUT/ })
    .check();
  await confirm.click();
  await expect(
    page.getByRole("heading", { name: "Entrada registrada" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Salida", exact: true }).click();
  await page
    .getByRole("button", { name: "Registrar salida", exact: true })
    .click();
  await expect(page.locator(".present-member")).toHaveCount(0);
  await page.getByRole("button", { name: "Entrada", exact: true }).click();
  await page
    .getByRole("checkbox", { name: /Confirmo que el nombre y RUT/ })
    .check();
  await confirm.click();
  await expect(page.locator(".present-member")).toHaveCount(1);
  await page.goto("/estudiante/reservas");
  await expect(page.locator(".visit-details")).toContainText(
    "2 visitas registradas",
  );
});

test("deportista con varias reservas diarias y estudiante con una sola", async ({
  page,
}) => {
  await page.goto("/estudiante/horarios");
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page.getByRole("button", { name: "Confirmar reserva" }).click();
  await expect(page.locator(".reservation-row")).toHaveCount(1);
  await page.goto("/estudiante/horarios");
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page.getByRole("button", { name: "Confirmar reserva" }).click();
  await expect(page.locator(".reservation-row")).toHaveCount(2);
  await page.goto("/estudiante/horarios?usuario=student-2");
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page.getByRole("button", { name: "Confirmar reserva" }).click();
  await expect(page.locator(".reservation-row")).toHaveCount(1);
  await page.goto("/estudiante/horarios?usuario=student-2");
  await expect(
    page.getByRole("button", { name: "Reservar", exact: true }),
  ).toHaveCount(0);
});

test("sesión de rama administrativa y filtros de usuarios y KPIs", async ({
  page,
}) => {
  await page.goto("/admin/grupos");
  await page
    .getByLabel("Rama deportiva", { exact: true })
    .selectOption("Fútbol");
  await page
    .getByLabel("Horario", { exact: true })
    .selectOption("2026-11-18-1");
  await page.getByRole("checkbox", { name: /Alex Martínez/ }).check();
  await page.getByRole("checkbox", { name: /Valentina Rojas/ }).check();
  await page.getByRole("button", { name: "Confirmar sesión de rama" }).click();
  await expect(page.locator(".group-record")).toHaveCount(1);
  await expect(page.locator(".group-record")).toContainText("2 deportistas");
  await page.goto("/admin/inicio");
  await page
    .getByLabel("Rama deportiva", { exact: true })
    .selectOption("Fútbol");
  await expect(
    page.locator(".metric-card").first().locator(".metric-value"),
  ).toHaveText("2");
  await page
    .getByLabel("Rama deportiva", { exact: true })
    .selectOption("Básquetbol");
  await expect(
    page.locator(".metric-card").first().locator(".metric-value"),
  ).toHaveText("0");
  await page.goto("/admin/usuarios");
  await page
    .getByLabel("Filtrar por rama deportiva")
    .selectOption("Básquetbol");
  await expect(page.locator("tbody tr")).toHaveCount(1);
  await expect(page.locator("tbody")).toContainText("Martina López");
  expect(
    await page.evaluate(
      () =>
        document.documentElement.scrollWidth <=
        document.documentElement.clientWidth,
    ),
  ).toBe(true);
});

test("inasistencia: aviso al usuario y acuse persistente sin bloquearlo", async ({
  page,
}) => {
  const state = reserve(
    structuredClone(initialState),
    "student-1",
    slotsFor("2026-11-18")[0],
    now,
  );
  await seed(page, state);
  await page.clock.setFixedTime(new Date("2026-11-18T10:00:00"));
  await page.goto("/estudiante/inicio");
  await expect(
    page.getByRole("dialog", { name: "Tienes reservas sin asistencia" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Entendido" }).click();
  await page.reload();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await page.goto("/estudiante/reservas");
  await expect(page.locator(".reservation-row .badge")).toHaveText(
    "No asistió",
  );
});

test("Excel real: importar con vista previa y exportar un libro legible", async ({
  page,
}) => {
  const buffer = await writeXlsxFile([
    IMPORT_HEADERS,
    [
      "22.222.222-2",
      "Fernanda",
      "Pérez",
      "fernanda@example.invalid",
      "Deportista",
      "Natación",
    ],
  ]).toBuffer();
  await page.goto("/admin/archivos");
  await page.getByLabel("Archivo de usuarios (.xlsx)").setInputFiles({
    name: "usuarios.xlsx",
    mimeType:
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    buffer,
  });
  await expect(page.locator(".import-preview")).toContainText("1 válidas");
  await page.getByRole("button", { name: "Confirmar importación (1)" }).click();
  await expect(
    page.getByText("Usuarios creados con activación pendiente.", {
      exact: true,
    }),
  ).toBeVisible();
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Exportar todo (.xlsx)" }).click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/UCN-Gym.*\.xlsx$/);
  const rows = await readSheet((await file.path())!, "Usuarios");
  expect(
    rows.some(
      (row) =>
        row.includes("fernanda@example.invalid") && row.includes("Natación"),
    ),
  ).toBe(true);
  await page.goto("/admin/usuarios");
  await page.getByRole("textbox", { name: "Buscar usuarios" }).fill("Fernanda");
  await expect(page.locator("tbody")).toContainText("Activación pendiente");
});

test("recuperación: no afirma un envío si falla la API", async ({ page }) => {
  await page.route("**/api/auth/password-resets", (route) =>
    route.fulfill({ status: 503, contentType: "application/json", body: "{}" }),
  );
  await page.goto("/recuperar-contrasena");
  await page.getByLabel("Correo registrado").fill("cuenta@example.invalid");
  await page.getByRole("button", { name: "Enviar enlace" }).click();
  await expect(page.getByRole("alert")).toContainText("No se pudo procesar");
  await expect(page.getByRole("status")).toHaveCount(0);
});

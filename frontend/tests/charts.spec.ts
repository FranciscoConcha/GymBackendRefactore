import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { login, mockApi } from "./api-fixture";
import { localDate } from "../src/domain";

test("gráficos con reservas reales del contrato, tablas y controles", async ({
  page,
}) => {
  const api = await mockApi(page);
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  const date = localDate(new Date());
  for (const [index, status] of (
    ["Reservado", "Cancelado", "Asistió"] as const
  ).entries()) {
    api.reservations.push({
      id: index + 1,
      userId: 1,
      userName: "Alex Soto",
      scheduleSlotId: 1,
      date,
      day: "Miércoles",
      startTime: "08:00",
      endTime: "09:30",
      zone: "Sala",
      capacity: 7,
      status,
      createdAt: new Date().toISOString(),
    });
  }
  await login(page, true);
  await expect(page.locator(".metric-card")).toHaveCount(4);
  await expect(page.locator(".activity-echart svg")).toBeVisible();
  await expect(page.locator(".demand-echart svg")).toBeVisible();
  const legend = page.getByRole("button", {
    name: "Cancelaciones",
    exact: true,
  });
  await legend.click();
  await expect(legend).toHaveAttribute("aria-pressed", "false");
  await legend.press("Enter");
  await expect(legend).toHaveAttribute("aria-pressed", "true");
  await page.getByRole("button", { name: "30 días", exact: true }).click();
  await expect(page.locator(".activity-echart.zoomable svg")).toBeVisible();
  await page.getByRole("button", { name: "Ampliar últimos 7 días" }).click();
  await page.getByRole("button", { name: "Restablecer zoom" }).click();
  await page.locator(".reservation-chart summary").click();
  const rows = page.locator(".reservation-chart .chart-data tbody tr");
  await expect(rows).toHaveCount(30);
  await expect(rows.last().locator("td").nth(0)).toHaveText("3");
  await expect(rows.last().locator("td").nth(1)).toHaveText("1");
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  expect(errors).toEqual([]);
});

test("sin datos, movimiento reducido y ajuste del contenedor", async ({
  page,
}) => {
  await mockApi(page);
  await page.emulateMedia({ reducedMotion: "reduce" });
  await login(page, true);
  await expect(
    page.getByRole("heading", { name: "Aún no hay demanda registrada" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Historial no disponible" }),
  ).toBeVisible();
  await expect(page.locator(".activity-echart svg")).toBeVisible();
  await page.setViewportSize({ width: 375, height: 812 });
  await expect
    .poll(() =>
      page
        .locator(".activity-echart")
        .evaluate((el) =>
          Math.abs(
            Number(el.querySelector("svg")?.getAttribute("width")) -
              el.clientWidth,
          ),
        ),
    )
    .toBeLessThan(2);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
});

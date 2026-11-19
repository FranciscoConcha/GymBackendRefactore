import { expect, test } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import {
  initialState,
  localDate,
  slotsFor,
  STORAGE_KEY,
  type DemoState,
} from "../src/domain";

test("ECharts: tooltips, series, zoom y datos accesibles", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  const date = localDate(new Date());
  const slot = slotsFor(date)[0];
  const now = new Date().toISOString();
  const state: DemoState = {
    ...structuredClone(initialState),
    reservations: [
      { id: "pending", memberId: "student-1", slot, status: "Reservada" },
      { id: "cancelled", memberId: "student-2", slot, status: "Cancelada" },
      {
        id: "inside",
        memberId: "student-2",
        slot,
        status: "En sala",
        entry: now,
      },
      {
        id: "completed",
        memberId: "student-1",
        slot,
        status: "Completada",
        entry: now,
        exit: now,
      },
    ],
  };
  await page.addInitScript(
    ({ key, state }) => localStorage.setItem(key, JSON.stringify(state)),
    { key: STORAGE_KEY, state },
  );
  await page.goto("/admin/inicio");
  const activity = page.locator(".activity-echart");
  await expect(activity.locator("svg")).toBeVisible();
  await expect(page.locator(".demand-echart svg")).toBeVisible();
  const bounds = await activity.boundingBox();
  await activity.hover({
    position: { x: 40 + ((bounds!.width - 52) * 6.5) / 7, y: 80 },
  });
  await expect(activity).toContainText("4 reservas");
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
  await expect(rows.last().locator("td").nth(0)).toHaveText("4");
  await expect(rows.last().locator("td").nth(1)).toHaveText("1");
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  expect(errors).toEqual([]);
});

test("ECharts se ajusta al contenedor y mantiene los controles con movimiento reducido", async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/admin/inicio");
  await expect(page.locator(".activity-echart svg")).toBeVisible();
  await page.setViewportSize({ width: 375, height: 812 });
  await expect
    .poll(async () => {
      const chart = page.locator(".activity-echart");
      return chart.evaluate((element) =>
        Math.abs(
          Number(element.querySelector("svg")?.getAttribute("width")) -
            element.clientWidth,
        ),
      );
    })
    .toBeLessThan(2);
  expect(
    await page.evaluate(
      () =>
        document.documentElement.scrollWidth <=
        document.documentElement.clientWidth,
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "Reservas", exact: true }).click();
  await expect(
    page.getByRole("button", { name: "Reservas", exact: true }),
  ).toHaveAttribute("aria-pressed", "false");
});

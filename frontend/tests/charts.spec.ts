import { expect, test } from "@playwright/test";
import { mockApi } from "./api-fixture";
test("métricas del servidor con filtros y aforo global", async ({ page }) => {
  const mock = await mockApi(page, { role: "Admin" });
  await page.goto("/admin/inicio");
  await expect(
    page.getByRole("heading", { name: "Panel general" }),
  ).toBeVisible();
  await expect(
    page.locator(".metric-card").filter({ hasText: "Permanencia media" }),
  ).toContainText("42 min");
  await expect(
    page.locator(".metric-card").filter({ hasText: "Aforo actual" }),
  ).toContainText("0 / 30");
  await page.getByLabel("Rama deportiva", { exact: true }).selectOption("1");
  await page
    .getByLabel("Categoría", { exact: true })
    .selectOption("Deportista");
  await page.getByRole("button", { name: "30 días", exact: true }).click();
  await expect
    .poll(() => mock.calls.filter((c) => c.path === "/admin/metrics").length)
    .toBeGreaterThan(2);
  await expect
    .poll(
      () => mock.calls.filter((c) => c.path === "/admin/metrics").at(-1)?.query,
    )
    .toMatchObject({
      branchId: "1",
      category: "Deportista",
      from: expect.any(String),
      to: expect.any(String),
    });
  await expect(
    page.locator(".metric-card").filter({ hasText: "Aforo actual" }),
  ).toContainText("0 / 30");
  await expect(
    page.getByRole("heading", { name: "Demanda por horario" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Demanda por rama" }),
  ).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "Usuarios habilitados por categoría" }),
  ).toBeVisible();
  await expect(
    page.getByRole("table").filter({
      has: page.getByRole("columnheader", { name: "Rama", exact: true }),
    }),
  ).toContainText("Fútbol");
  expect(mock.unknown).toEqual([]);
});
test("error de métricas se muestra sin datos inventados", async ({ page }) => {
  const mock = await mockApi(page, { role: "Admin" });
  mock.failNext("/admin/metrics", 500, "Métricas no disponibles.");
  await page.goto("/admin/inicio");
  await expect(page.getByRole("alert")).toHaveText("Métricas no disponibles.");
  await expect(page.locator(".metric-card")).toHaveCount(0);
});

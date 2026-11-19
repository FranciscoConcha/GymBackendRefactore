import { afterEach, expect, it, vi } from "vitest";
import { api, request } from "./api";
afterEach(() => vi.unstubAllGlobals());
it.each(["reservation", "group", "entry", "import"])(
  "envía Idempotency-Key y CSRF para %s",
  async (kind) => {
    const fetcher = vi.fn().mockResolvedValue(new Response("{}"));
    vi.stubGlobal("fetch", fetcher);
    if (kind === "reservation")
      await api.reserve(3, "2026-11-19", "attempt-key");
    if (kind === "group")
      await api.createGroup(
        {
          sportBranchId: 1,
          scheduleSlotId: 3,
          date: "2026-11-19",
          mode: "Cupos",
          seats: 4,
        },
        "attempt-key",
      );
    if (kind === "entry") await api.entry("signed-token", "TNE", "attempt-key");
    if (kind === "import") await api.confirmImport(9, "attempt-key");
    expect(fetcher.mock.calls[0][1].headers).toMatchObject({
      "X-Gym-Csrf": "1",
      "Idempotency-Key": "attempt-key",
    });
  },
);
it("sube multipart sin imponer Content-Type JSON", async () => {
  const fetcher = vi.fn().mockResolvedValue(new Response("{}"));
  vi.stubGlobal("fetch", fetcher);
  const file = new File(["xlsx"], "usuarios.xlsx");
  await api.previewImport(file);
  const options = fetcher.mock.calls[0][1];
  expect(options.body).toBeInstanceOf(FormData);
  expect(options.body.get("file")).toBe(file);
  expect(options.headers["Content-Type"]).toBeUndefined();
  expect(options.headers["X-Gym-Csrf"]).toBe("1");
});
it("descarga blob con nombre UTF-8 de Content-Disposition", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(
      new Response("xlsx-data", {
        headers: {
          "Content-Disposition":
            "attachment; filename=fallback.xlsx; filename*=UTF-8''gimnasio%20UCN.xlsx",
        },
      }),
    ),
  );
  const result = await api.export({ from: "2026-11-01" });
  expect(result.filename).toBe("gimnasio UCN.xlsx");
  expect(await result.blob.text()).toBe("xlsx-data");
});
it("acepta respuestas vacías 204", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(new Response(null, { status: 204 })),
  );
  await expect(request("/test", { method: "DELETE" })).resolves.toBeUndefined();
});

it.each([{ token: "signed-exit" }, { userId: 7 }])(
  "env?a salida exclusiva %j con CSRF y clave",
  async (body) => {
    const fetcher = vi.fn().mockResolvedValue(new Response("{}"));
    vi.stubGlobal("fetch", fetcher);
    await api.exit(body, "exit-key");
    expect(fetcher.mock.calls[0][0]).toBe("/api/v1/access/exit");
    const options = fetcher.mock.calls[0][1];
    expect(JSON.parse(options.body)).toEqual(body);
    expect(options.headers).toMatchObject({
      "X-Gym-Csrf": "1",
      "Idempotency-Key": "exit-key",
    });
  },
);

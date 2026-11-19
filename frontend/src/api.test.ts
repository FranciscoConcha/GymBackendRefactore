import { afterEach, expect, it, vi } from "vitest";
import { api, ApiError, onUnauthorized, request } from "./api";

afterEach(() => vi.unstubAllGlobals());

it.each(["GET", "POST", "PUT", "PATCH", "DELETE", "post"])(
  "envía credenciales y CSRF según %s",
  async (method) => {
    const fetcher = vi.fn().mockResolvedValue(new Response("{}"));
    vi.stubGlobal("fetch", fetcher);
    await request("/test", { method });
    const [url, options] = fetcher.mock.calls[0];
    expect(url).toBe("/api/v1/test");
    expect(options.credentials).toBe("same-origin");
    expect(options.headers.Accept).toBe("application/json");
    expect(options.headers["X-Gym-Csrf"]).toBe(
      method === "GET" ? undefined : "1",
    );
    expect(options.headers["Content-Type"]).toBeUndefined();
  },
);

it.each([
  [403, { message: "Sin permiso" }, "Sin permiso"],
  [409, { detail: "Sin cupo", title: "Conflicto" }, "Sin cupo"],
  [429, { title: "Demasiados intentos" }, "Demasiados intentos"],
  [500, null, "Error HTTP 500"],
])("interpreta error %s", async (status, body, message) => {
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(
      new Response(body ? JSON.stringify(body) : "no JSON", {
        status: status as number,
      }),
    ),
  );
  await expect(request("/test")).rejects.toMatchObject({
    name: "ApiError",
    status,
    message,
  });
});

it("notifica 401 global, exceptúa comprobación y login, y permite desuscribir", async () => {
  vi.stubGlobal(
    "fetch",
    vi
      .fn()
      .mockImplementation(() =>
        Promise.resolve(
          new Response('{"message":"Sin sesión"}', { status: 401 }),
        ),
      ),
  );
  const handler = vi.fn();
  const unsubscribe = onUnauthorized(handler);
  try {
    await expect(api.me()).rejects.toBeInstanceOf(ApiError);
    await expect(api.login(false, "123", "clave")).rejects.toBeInstanceOf(
      ApiError,
    );
    expect(handler).not.toHaveBeenCalled();
    await expect(api.qr()).rejects.toMatchObject({ status: 401 });
    expect(handler).toHaveBeenCalledTimes(1);
    unsubscribe();
    await expect(api.qr()).rejects.toBeInstanceOf(ApiError);
    expect(handler).toHaveBeenCalledTimes(1);
  } finally {
    unsubscribe();
  }
});

it("serializa el cuerpo de login de cada portal", async () => {
  const fetcher = vi
    .fn()
    .mockImplementation(() => Promise.resolve(new Response("{}")));
  vi.stubGlobal("fetch", fetcher);
  await api.login(false, "12.345.678-9", "clave");
  await api.login(true, "admin", "clave");
  expect(fetcher.mock.calls[0][0]).toBe("/api/v1/auth/login");
  expect(JSON.parse(fetcher.mock.calls[0][1].body)).toEqual({
    rut: "12.345.678-9",
    password: "clave",
  });
  expect(fetcher.mock.calls[1][0]).toBe("/api/v1/auth/admin-login");
  expect(JSON.parse(fetcher.mock.calls[1][1].body)).toEqual({
    username: "admin",
    password: "clave",
  });
  expect(fetcher.mock.calls[0][1].headers["Content-Type"]).toBe(
    "application/json",
  );
});

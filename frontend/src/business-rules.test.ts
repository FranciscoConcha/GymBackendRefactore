import { afterEach, expect, it, vi } from "vitest";
import { api, onPasswordRequired } from "./api";
afterEach(() => vi.unstubAllGlobals());
it("conserva 409 de cupo y 422 de idempotencia exactamente", async () => {
  for (const [status, message] of [
    [409, "Se alcanzó el aforo máximo de la sala."],
    [422, "La clave de idempotencia ya se usó con otra solicitud."],
  ] as const) {
    vi.stubGlobal(
      "fetch",
      vi
        .fn()
        .mockResolvedValue(
          new Response(JSON.stringify({ message }), { status }),
        ),
    );
    await expect(
      api.reserve(1, "2026-11-19", "same-key"),
    ).rejects.toMatchObject({ status, message });
  }
});
it("redirige el 403 de contraseña temporal desde cualquier endpoint", async () => {
  const handler = vi.fn();
  const off = onPasswordRequired(handler);
  vi.stubGlobal(
    "fetch",
    vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          message: "Debe cambiar su contraseña temporal antes de continuar.",
        }),
        { status: 403 },
      ),
    ),
  );
  try {
    await expect(api.current()).rejects.toMatchObject({ status: 403 });
    expect(handler).toHaveBeenCalledOnce();
  } finally {
    off();
  }
});

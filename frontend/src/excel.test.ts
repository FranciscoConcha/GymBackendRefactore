import { describe, expect, it } from "vitest";
import {
  confirmUserImport,
  exportSheets,
  IMPORT_HEADERS,
  validateUserRows,
} from "./excel";
import { initialState, rutCheckDigit } from "./domain";

const rut = (body: string) => body + rutCheckDigit(body);
const row = () => [
  rut("22222222"),
  "Fernanda",
  "Pérez",
  "fernanda@example.invalid",
  "Deportista",
  "Natación",
];
describe("Excel administrativo", () => {
  it("previsualiza sin modificar el estado y crea una cuenta pendiente", () => {
    const state = structuredClone(initialState);
    const preview = validateUserRows([IMPORT_HEADERS, row()], state);
    expect(preview.errors).toHaveLength(0);
    expect(state.members).toHaveLength(initialState.members.length);
    const updated = confirmUserImport(state, preview, "admin");
    expect(updated.members.at(-1)?.accountStatus).toBe(
      "Pendiente de activación",
    );
    expect(updated.members.at(-1)?.branch).toBe("Natación");
  });
  it("rechaza columnas faltantes, errores por fila, ramas faltantes y RUT inválido", () => {
    expect(() => validateUserRows([["Nombre"]], initialState)).toThrow(
      "Faltan columnas",
    );
    const missingBranch = row();
    missingBranch[5] = "";
    const wrongRut = row();
    wrongRut[0] = "123456780";
    const preview = validateUserRows(
      [IMPORT_HEADERS, missingBranch, wrongRut],
      initialState,
    );
    expect(preview.errors.map((value) => value.row)).toEqual([2, 3]);
    expect(() => confirmUserImport(initialState, preview, "admin")).toThrow(
      "Corrige",
    );
  });
  it("no permite duplicados ni fórmulas ni roles privilegiados como categoría", () => {
    const formula = row();
    formula[1] = '=HYPERLINK("https://example.invalid")';
    const role = row();
    role[4] = "Administrador";
    expect(
      validateUserRows(
        [IMPORT_HEADERS, row(), row(), formula, role],
        initialState,
      ).errors,
    ).toHaveLength(3);
  });
  it("revalida duplicados al confirmar y exige administrador", () => {
    const preview = validateUserRows([IMPORT_HEADERS, row()], initialState);
    expect(() => confirmUserImport(initialState, preview, "reception")).toThrow(
      "Solo los administradores",
    );
    const updated = confirmUserImport(initialState, preview, "admin");
    expect(() => confirmUserImport(updated, preview, "admin")).toThrow(
      "cambiaron",
    );
    expect(() => exportSheets(initialState, "student")).toThrow(
      "Solo los administradores",
    );
  });
  it("exporta hojas operativas sin secretos y neutraliza expresiones", () => {
    const state = structuredClone(initialState);
    state.members[0].name = "=EXPRESION";
    const sheets = exportSheets(state, "admin");
    expect(sheets.map((value) => value.sheet)).toEqual([
      "Usuarios",
      "Sesiones",
      "Reservas",
      "Visitas",
      "Grupos",
      "Inasistencias",
    ]);
    expect(JSON.stringify(sheets)).not.toMatch(/PasswordHash|password|UCN:/);
    expect(JSON.stringify(sheets)).toContain("'=EXPRESION");
  });
});

import {
  CATEGORIES,
  formatRut,
  getDays,
  isValidRut,
  normalizeRut,
  slotsFor,
  type DemoState,
  type Member,
  type Role,
} from "./domain";
import type { Sheet, SheetData } from "write-excel-file/browser";

export const IMPORT_HEADERS = [
  "RUT",
  "Nombre",
  "Apellido",
  "Correo",
  "Categoria",
  "Rama",
];
export interface ImportPreview {
  members: Member[];
  errors: { row: number; message: string }[];
  total: number;
}
const headerKey = (value: unknown) =>
  String(value ?? "")
    .normalize("NFD")
    .replace(/\p{Diacritic}/gu, "")
    .trim()
    .toLowerCase();
export function validateUserRows(
  rows: unknown[][],
  state: DemoState,
): ImportPreview {
  if (!rows.length) throw new Error("El archivo está vacío.");
  if (rows.length > 2001)
    throw new Error("El máximo es de 2.000 usuarios por archivo.");
  const headers = rows[0].map(headerKey);
  const required = ["rut", "nombre", "apellido", "correo", "categoria"];
  if (required.some((header) => !headers.includes(header)))
    throw new Error("Faltan columnas. Usa la plantilla de usuarios.");
  if (new Set(headers.filter(Boolean)).size !== headers.filter(Boolean).length)
    throw new Error("Hay columnas duplicadas.");
  const errors: ImportPreview["errors"] = [],
    members: Member[] = [];
  const ruts = new Set(
    state.members.map((member) => normalizeRut(member.rut)).filter(Boolean),
  );
  const emails = new Set(
    state.members.map((member) => member.email.toLowerCase()),
  );
  let total = 0;
  rows.slice(1).forEach((row, index) => {
    if (row.every((value) => value == null || String(value).trim() === ""))
      return;
    total++;
    const cell = (header: string) =>
      String(row[headers.indexOf(header)] ?? "").trim();
    const rut = cell("rut"),
      firstName = cell("nombre"),
      lastName = cell("apellido"),
      email = cell("correo").toLowerCase(),
      category = cell("categoria"),
      branch = cell("rama");
    const fail = (message: string) => errors.push({ row: index + 2, message });
    if (
      [rut, firstName, lastName, email, category, branch].some((value) =>
        /^[=+@]/.test(value),
      )
    ) {
      fail("No se permiten fórmulas ni expresiones.");
      return;
    }
    if (!isValidRut(rut)) {
      fail("RUT inválido o sin dígito verificador.");
      return;
    }
    if (!firstName || !lastName || `${firstName} ${lastName}`.length > 100) {
      fail("Nombre y apellido son obligatorios (máximo 100 caracteres).");
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 200) {
      fail("Correo inválido.");
      return;
    }
    if (!CATEGORIES.includes(category as (typeof CATEGORIES)[number])) {
      fail("Categoría no permitida.");
      return;
    }
    if (category === "Deportista" && !branch) {
      fail("Los deportistas deben indicar una rama.");
      return;
    }
    if (ruts.has(normalizeRut(rut)) || emails.has(email)) {
      fail("RUT o correo duplicado en el archivo o en usuarios existentes.");
      return;
    }
    ruts.add(normalizeRut(rut));
    emails.add(email);
    members.push({
      id: crypto.randomUUID(),
      rut: formatRut(rut),
      name: `${firstName} ${lastName}`,
      email,
      category,
      branch: category === "Deportista" ? branch : undefined,
      enabled: true,
      accountStatus: "Pendiente de activación",
    });
  });
  return { members, errors, total };
}
export async function previewUserFile(
  file: File,
  state: DemoState,
): Promise<ImportPreview> {
  if (!file.name.toLowerCase().endsWith(".xlsx"))
    throw new Error("Selecciona un archivo .xlsx.");
  if (file.size > 5 * 1024 * 1024)
    throw new Error("El tamaño máximo es de 5 MB.");
  const { readSheet } = await import("read-excel-file/browser");
  return validateUserRows(await readSheet(file, 1), state);
}
export function confirmUserImport(
  state: DemoState,
  preview: ImportPreview,
  role: Role,
): DemoState {
  if (role !== "admin")
    throw new Error("Solo los administradores pueden importar usuarios.");
  if (!preview.members.length || preview.errors.length)
    throw new Error("Corrige todos los errores antes de importar.");
  const seenRuts = new Set(
    state.members.map((member) => normalizeRut(member.rut)),
  );
  const seenEmails = new Set(
    state.members.map((member) => member.email.toLowerCase()),
  );
  for (const member of preview.members) {
    if (
      seenRuts.has(normalizeRut(member.rut)) ||
      seenEmails.has(member.email.toLowerCase())
    )
      throw new Error(
        "Los usuarios cambiaron desde la vista previa. Vuelve a cargar el archivo.",
      );
    seenRuts.add(normalizeRut(member.rut));
    seenEmails.add(member.email.toLowerCase());
  }
  return { ...state, members: [...state.members, ...preview.members] };
}
type CellValue = string | number;
function safeCell(value: CellValue): CellValue {
  return typeof value === "string" && /^[=+@-]/.test(value)
    ? `'${value}`
    : value;
}
function sheetData(headers: string[], rows: CellValue[][]): SheetData {
  return [
    headers.map((value) => ({
      value,
      fontWeight: "bold" as const,
      textColor: "#FFFFFF",
      backgroundColor: "#0B0744",
    })),
    ...rows.map((row) => row.map(safeCell)),
  ];
}
export function exportSheets(state: DemoState, role: Role): Sheet<Blob>[] {
  if (role !== "admin")
    throw new Error("Solo los administradores pueden exportar registros.");
  const user = (id: string) => state.members.find((member) => member.id === id);
  const reservations = state.reservations;
  const sessions = [
    ...new Map(
      [
        ...getDays().flatMap(slotsFor),
        ...reservations.map((reservation) => reservation.slot),
      ].map((slot) => [slot.id, slot]),
    ).values(),
  ];
  return [
    {
      sheet: "Usuarios",
      data: sheetData(
        [...IMPORT_HEADERS, "Estado", "Cuenta"],
        state.members.map((member) => {
          const [firstName, ...lastName] = member.name.split(" ");
          return [
            member.rut,
            firstName,
            lastName.join(" "),
            member.email,
            member.category,
            member.branch ?? "",
            member.enabled ? "Habilitado" : "Deshabilitado",
            member.accountStatus ?? "Activa",
          ];
        }),
      ),
    },
    {
      sheet: "Sesiones",
      data: sheetData(
        ["ID", "Fecha", "Inicio", "Fin", "Capacidad"],
        sessions.map((slot) => [
          slot.id,
          slot.date,
          slot.start,
          slot.end,
          slot.capacity,
        ]),
      ),
    },
    {
      sheet: "Reservas",
      data: sheetData(
        [
          "ID",
          "RUT",
          "Nombre completo",
          "Rama",
          "Fecha",
          "Inicio",
          "Fin",
          "Estado",
          "Creacion UTC",
          "Grupo",
        ],
        reservations.map((reservation) => [
          reservation.id,
          user(reservation.memberId)?.rut ?? "",
          user(reservation.memberId)?.name ?? "",
          reservation.branch ?? "",
          reservation.slot.date,
          reservation.slot.start,
          reservation.slot.end,
          reservation.status,
          reservation.createdAt ?? "",
          reservation.groupId ?? "",
        ]),
      ),
    },
    {
      sheet: "Visitas",
      data: sheetData(
        [
          "Reserva",
          "RUT",
          "Nombre completo",
          "Entrada UTC",
          "Salida UTC",
          "Operador",
          "Documento",
        ],
        reservations.flatMap((reservation) =>
          (reservation.visits ?? []).map((visit) => [
            reservation.id,
            user(reservation.memberId)?.rut ?? "",
            user(reservation.memberId)?.name ?? "",
            visit.entryAt,
            visit.exitAt ?? "",
            visit.operatorRole,
            visit.document,
          ]),
        ),
      ),
    },
    {
      sheet: "Grupos",
      data: sheetData(
        ["ID", "Rama", "Fecha", "Inicio", "Participantes", "Creacion UTC"],
        (state.groups ?? []).map((group) => [
          group.id,
          group.branch,
          group.slot.date,
          group.slot.start,
          group.memberIds.map((id) => user(id)?.rut ?? id).join(", "),
          group.createdAt,
        ]),
      ),
    },
    {
      sheet: "Inasistencias",
      data: sheetData(
        ["Reserva", "RUT", "Nombre completo", "Fecha", "Leida UTC"],
        (state.alerts ?? []).map((alert) => [
          alert.reservationId,
          user(alert.memberId)?.rut ?? "",
          user(alert.memberId)?.name ?? "",
          alert.createdAt,
          alert.readAt ?? "",
        ]),
      ),
    },
  ];
}
export async function downloadExport(
  state: DemoState,
  role: Role,
): Promise<void> {
  const sheets = exportSheets(state, role);
  const { default: writeXlsxFile } = await import("write-excel-file/browser");
  await writeXlsxFile(sheets).toFile(
    `UCN-Gym-${new Date().toISOString().slice(0, 10)}.xlsx`,
  );
}
export async function downloadTemplate(): Promise<void> {
  const { default: writeXlsxFile } = await import("write-excel-file/browser");
  await writeXlsxFile(sheetData(IMPORT_HEADERS, [])).toFile(
    "Plantilla-usuarios-UCN-Gym.xlsx",
  );
}

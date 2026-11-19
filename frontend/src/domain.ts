export type Role = "student" | "admin" | "reception";
export type Category = "Deportista" | "Estudiante" | "Funcionario" | "Externo";
export const CATEGORIES: Category[] = [
  "Deportista",
  "Estudiante",
  "Funcionario",
  "Externo",
];
export const ROOM_CAPACITY = 30;
export type ReservationStatus =
  | "Reservada"
  | "En sala"
  | "Fuera de sala"
  | "Completada"
  | "Cancelada"
  | "No asistió";
export type IdentityDocument = "Carnet" | "TNE" | "Credencial universitaria";

export interface Member {
  id: string;
  name: string;
  rut: string;
  email: string;
  category: string;
  branch?: string;
  enabled: boolean;
  accountStatus?: "Activa" | "Pendiente de activación";
  weeklyLimit?: number;
}
export interface Slot {
  id: string;
  date: string;
  start: string;
  end: string;
  capacity: number;
  occupied: number;
}
export interface Visit {
  id: string;
  entryAt: string;
  exitAt?: string;
  operatorRole: Role;
  exitOperatorRole?: Role;
  document: IdentityDocument;
}
export interface Reservation {
  id: string;
  memberId: string;
  slot: Slot;
  status: ReservationStatus;
  createdAt?: string;
  entry?: string;
  exit?: string;
  visits?: Visit[];
  groupId?: string;
  branch?: string;
}
export interface GroupBooking {
  id: string;
  branch: string;
  memberIds: string[];
  slot: Slot;
  createdAt: string;
}
export interface AttendanceAlert {
  id: string;
  memberId: string;
  reservationId: string;
  createdAt: string;
  readAt?: string;
}
export interface DemoState {
  members: Member[];
  reservations: Reservation[];
  groups?: GroupBooking[];
  alerts?: AttendanceAlert[];
}
export const STORAGE_KEY = "ucn-gym:demo:v1";
export const DEMO_MEMBER = "student-1";

export function rutCheckDigit(body: string): string {
  let total = 0,
    factor = 2;
  for (let index = body.length - 1; index >= 0; index--) {
    total += Number(body[index]) * factor;
    factor = factor === 7 ? 2 : factor + 1;
  }
  const result = 11 - (total % 11);
  return result === 11 ? "0" : result === 10 ? "K" : String(result);
}
export function normalizeRut(value: string): string {
  return value.replace(/[.\s-]/g, "").toUpperCase();
}
export function isValidRut(value: string): boolean {
  const rut = normalizeRut(value);
  return (
    /^\d{7,8}[0-9K]$/.test(rut) &&
    rutCheckDigit(rut.slice(0, -1)) === rut.at(-1)
  );
}
export function formatRut(value: string): string {
  const rut = normalizeRut(value);
  if (!rut) return "";
  return `${rut.slice(0, -1).replace(/\B(?=(\d{3})+(?!\d))/g, ".")}-${rut.at(-1)}`;
}
const fixtureRut = (body: string) => formatRut(body + rutCheckDigit(body));
export const initialState: DemoState = {
  members: [
    {
      id: DEMO_MEMBER,
      name: "Alex Martínez",
      rut: fixtureRut("12345678"),
      email: "alex@example.invalid",
      category: "Deportista",
      branch: "Fútbol",
      enabled: true,
      accountStatus: "Activa",
    },
    {
      id: "student-2",
      name: "Camila Rojas",
      rut: fixtureRut("11111111"),
      email: "camila@example.invalid",
      category: "Estudiante",
      enabled: true,
      accountStatus: "Activa",
    },
    {
      id: "staff-1",
      name: "Daniel Soto",
      rut: fixtureRut("20000001"),
      email: "daniel@example.invalid",
      category: "Funcionario",
      enabled: false,
      accountStatus: "Activa",
    },
    {
      id: "athlete-2",
      name: "Valentina Rojas",
      rut: fixtureRut("20000002"),
      email: "valentina@example.invalid",
      category: "Deportista",
      branch: "Fútbol",
      enabled: true,
      accountStatus: "Activa",
    },
    {
      id: "athlete-3",
      name: "Diego Araya",
      rut: fixtureRut("20000003"),
      email: "diego@example.invalid",
      category: "Deportista",
      branch: "Fútbol",
      enabled: true,
      accountStatus: "Activa",
    },
    {
      id: "athlete-4",
      name: "Martina López",
      rut: fixtureRut("20000004"),
      email: "martina@example.invalid",
      category: "Deportista",
      branch: "Básquetbol",
      enabled: true,
      accountStatus: "Activa",
    },
  ],
  reservations: [],
  groups: [],
  alerts: [],
};
export function categoryPriority(category: string): number {
  const index = CATEGORIES.indexOf(category as Category);
  return index < 0 ? 4 : index;
}
export function localDate(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}
export function getDays(today = new Date()): string[] {
  const dates: string[] = [];
  const day = new Date(today.getFullYear(), today.getMonth(), today.getDate());
  while (dates.length < 6) {
    if (day.getDay() !== 0) dates.push(localDate(day));
    day.setDate(day.getDate() + 1);
  }
  return dates;
}
export function slotsFor(date: string): Slot[] {
  return [
    ["08:00", "09:30", 7],
    ["09:30", "11:00", 12],
    ["11:00", "12:30", 30],
    ["14:00", "15:30", 9],
    ["15:30", "17:00", 16],
    ["17:00", "18:30", 4],
  ].map(([start, end, occupied], index) => ({
    id: `${date}-${index}`,
    date,
    start: String(start),
    end: String(end),
    capacity: ROOM_CAPACITY,
    occupied: Number(occupied),
  }));
}
export function shortDate(
  value: string,
  options?: Intl.DateTimeFormatOptions,
): string {
  return new Intl.DateTimeFormat(
    "es-CL",
    options ?? { weekday: "long", day: "numeric", month: "long" },
  ).format(new Date(`${value}T12:00:00`));
}
export function slotStart(slot: Slot): Date {
  return new Date(`${slot.date}T${slot.start}:00`);
}
export function slotEnd(slot: Slot): Date {
  return new Date(`${slot.date}T${slot.end}:00`);
}
export function isPresent(reservation: Reservation): boolean {
  return reservation.visits?.length
    ? reservation.visits.some((visit) => !visit.exitAt)
    : reservation.status === "En sala";
}
export function hasAttended(reservation: Reservation): boolean {
  return (
    Boolean(reservation.entry || reservation.visits?.length) ||
    ["En sala", "Fuera de sala", "Completada"].includes(reservation.status)
  );
}
export function presentReservations(state: DemoState): Reservation[] {
  return [
    ...new Map(
      state.reservations
        .filter(isPresent)
        .map((reservation) => [reservation.memberId, reservation]),
    ).values(),
  ];
}
export function remaining(slot: Slot, state: DemoState): number {
  return Math.max(
    0,
    slot.capacity -
      slot.occupied -
      state.reservations.filter(
        (reservation) =>
          reservation.slot.id === slot.id && reservation.status !== "Cancelada",
      ).length,
  );
}
function weekKey(date: string): string {
  const monday = new Date(`${date}T12:00:00`);
  monday.setDate(monday.getDate() - ((monday.getDay() + 6) % 7));
  return localDate(monday);
}
export function validateReservation(
  state: DemoState,
  memberId: string,
  slot: Slot,
  now = new Date(),
): void {
  const member = state.members.find((value) => value.id === memberId);
  if (!member?.enabled) throw new Error("La cuenta no está habilitada.");
  if (!isValidRut(member.rut))
    throw new Error("La cuenta debe tener un RUT válido antes de reservar.");
  if (!CATEGORIES.includes(member.category as Category))
    throw new Error("Revisa la categoría del usuario con administración.");
  if (member.accountStatus === "Pendiente de activación")
    throw new Error("La cuenta está pendiente de activación.");
  if (member.category === "Deportista" && !member.branch?.trim())
    throw new Error("El deportista debe tener una rama deportiva.");
  if (slotEnd(slot) <= now) throw new Error("Esta sesión ya terminó.");
  if (remaining(slot, state) < 1)
    throw new Error("Este bloque no tiene cupos.");
  const mine = state.reservations.filter(
    (reservation) =>
      reservation.memberId === memberId && reservation.status !== "Cancelada",
  );
  if (mine.some((reservation) => reservation.slot.id === slot.id))
    throw new Error("Ya tienes una reserva en este bloque.");
  if (
    member.category === "Estudiante" &&
    mine.some((reservation) => reservation.slot.date === slot.date)
  )
    throw new Error("Los estudiantes pueden reservar una sesión por día.");
  if (
    member.category === "Estudiante" &&
    member.weeklyLimit != null &&
    mine.filter(
      (reservation) => weekKey(reservation.slot.date) === weekKey(slot.date),
    ).length >= member.weeklyLimit
  )
    throw new Error("Alcanzaste el límite semanal de tu plan.");
}
export function reserve(
  state: DemoState,
  memberId: string,
  slot: Slot,
  now = new Date(),
): DemoState {
  validateReservation(state, memberId, slot, now);
  const member = state.members.find((value) => value.id === memberId)!;
  return {
    ...state,
    reservations: [
      ...state.reservations,
      {
        id: crypto.randomUUID(),
        memberId,
        slot,
        status: "Reservada",
        createdAt: now.toISOString(),
        branch: member.branch,
        visits: [],
      },
    ],
  };
}
export function reserveGroup(
  state: DemoState,
  role: Role,
  branch: string,
  memberIds: string[],
  slot: Slot,
  now = new Date(),
): DemoState {
  if (role !== "admin")
    throw new Error("Solo un administrador puede reservar una sesión de rama.");
  const ids = [...new Set(memberIds)];
  if (!branch.trim() || !ids.length)
    throw new Error("Selecciona una rama y al menos un deportista.");
  if (slotStart(slot).getTime() - now.getTime() < 60 * 60 * 1000)
    throw new Error(
      "Las sesiones de rama requieren al menos una hora de anticipación.",
    );
  if (ids.length > remaining(slot, state))
    throw new Error("No hay cupos para todos los deportistas seleccionados.");
  for (const id of ids) {
    const member = state.members.find((value) => value.id === id);
    if (member?.category !== "Deportista" || member.branch !== branch)
      throw new Error(
        "Todos los participantes deben ser deportistas de la misma rama.",
      );
    validateReservation(state, id, slot, now);
  }
  const groupId = crypto.randomUUID();
  return {
    ...state,
    groups: [
      ...(state.groups ?? []),
      {
        id: groupId,
        branch,
        memberIds: ids,
        slot,
        createdAt: now.toISOString(),
      },
    ],
    reservations: [
      ...state.reservations,
      ...ids.map((memberId) => ({
        id: crypto.randomUUID(),
        memberId,
        slot,
        status: "Reservada" as const,
        createdAt: now.toISOString(),
        visits: [],
        branch,
        groupId,
      })),
    ],
  };
}
export function cancel(state: DemoState, id: string): DemoState {
  const reservation = state.reservations.find((value) => value.id === id);
  if (
    !reservation ||
    reservation.status !== "Reservada" ||
    hasAttended(reservation)
  )
    throw new Error("Solo puedes cancelar una reserva pendiente.");
  return {
    ...state,
    reservations: state.reservations.map((value) =>
      value.id === id ? { ...value, status: "Cancelada" } : value,
    ),
  };
}
export function ticket(id: string): string {
  return `UCN:${id}`;
}
export function reservationFromCode(
  state: DemoState,
  code: string,
): Reservation {
  const normalized = code.trim();
  const prefix = normalized.startsWith("UCN-DEMO:") ? "UCN-DEMO:" : "UCN:";
  if (!normalized.startsWith(prefix)) throw new Error("Código QR no válido.");
  const reservation = state.reservations.find(
    (value) => value.id === normalized.slice(prefix.length),
  );
  if (!reservation) throw new Error("No encontramos la reserva.");
  return reservation;
}
export function access(
  state: DemoState,
  code: string,
  action: "entry" | "exit",
  now = new Date(),
  operatorRole: Role = "admin",
  document: IdentityDocument = "Carnet",
): DemoState {
  if (!["admin", "reception"].includes(operatorRole))
    throw new Error("No tienes permisos para registrar accesos.");
  const reservation = reservationFromCode(state, code);
  const member = state.members.find(
    (value) => value.id === reservation.memberId,
  );
  if (action === "entry") {
    if (!member?.enabled) throw new Error("La cuenta no está habilitada.");
    if (!isValidRut(member.rut))
      throw new Error("El usuario debe tener un RUT válido antes de ingresar.");
    if (isPresent(reservation))
      throw new Error("La persona ya tiene una entrada abierta.");
    if (["Cancelada", "No asistió", "Completada"].includes(reservation.status))
      throw new Error("La reserva no está disponible para ingresar.");
    if (now.getTime() < slotStart(reservation.slot).getTime() - 15 * 60 * 1000)
      throw new Error("Puedes ingresar desde 15 minutos antes del inicio.");
    if (now >= slotEnd(reservation.slot))
      throw new Error("La sesión ya terminó.");
    if (presentReservations(state).length >= ROOM_CAPACITY)
      throw new Error("La sala alcanzó el aforo de 30 personas.");
    if (
      state.reservations.some(
        (value) =>
          value.memberId === reservation.memberId &&
          value.id !== reservation.id &&
          isPresent(value),
      )
    )
      throw new Error("La persona ya está dentro con otra sesión.");
  } else if (!isPresent(reservation))
    throw new Error("No hay una entrada abierta para esta reserva.");
  const visits = reservation.visits?.length
    ? reservation.visits
    : reservation.entry
      ? [
          {
            id: `legacy-${reservation.id}`,
            entryAt: reservation.entry,
            exitAt: reservation.exit,
            operatorRole,
            document,
          },
        ]
      : [];
  const timestamp = now.toISOString();
  const updatedVisits =
    action === "entry"
      ? [
          ...visits,
          {
            id: crypto.randomUUID(),
            entryAt: timestamp,
            operatorRole,
            document,
          },
        ]
      : visits.map((visit) =>
          visit.exitAt
            ? visit
            : { ...visit, exitAt: timestamp, exitOperatorRole: operatorRole },
        );
  return {
    ...state,
    reservations: state.reservations.map((value) =>
      value.id !== reservation.id
        ? value
        : {
            ...value,
            visits: updatedVisits,
            status:
              action === "entry"
                ? "En sala"
                : now < slotEnd(value.slot)
                  ? "Fuera de sala"
                  : "Completada",
            entry: value.entry ?? (action === "entry" ? timestamp : undefined),
            ...(action === "exit" ? { exit: timestamp } : {}),
          },
    ),
  };
}
export function reconcileAttendance(
  state: DemoState,
  now = new Date(),
): DemoState {
  let changed = false;
  const alerts = [...(state.alerts ?? [])];
  const reservations = state.reservations.map((reservation) => {
    if (
      slotEnd(reservation.slot) > now ||
      ["Cancelada", "Completada", "No asistió"].includes(reservation.status)
    )
      return reservation;
    changed = true;
    if (hasAttended(reservation))
      return { ...reservation, status: "Completada" as const };
    if (!alerts.some((alert) => alert.reservationId === reservation.id))
      alerts.push({
        id: `no-show-${reservation.id}`,
        memberId: reservation.memberId,
        reservationId: reservation.id,
        createdAt: now.toISOString(),
      });
    return { ...reservation, status: "No asistió" as const };
  });
  return changed ? { ...state, reservations, alerts } : state;
}
export function migrateState(parsed: DemoState): DemoState {
  const members: Member[] = parsed.members.map((member) => {
    const fixture = initialState.members.find(
      (value) => value.id === member.id,
    );
    return {
      ...member,
      rut: member.rut ?? fixture?.rut ?? "",
      accountStatus: member.accountStatus ?? ("Activa" as const),
      branch: member.category === "Deportista" ? member.branch : undefined,
    };
  });
  // Keep existing accounts and reservations; add the new athlete profiles only for this local prototype.
  for (const fixture of initialState.members.filter((member) =>
    member.id.startsWith("athlete-"),
  ))
    if (
      !members.some(
        (member) =>
          member.id === fixture.id ||
          normalizeRut(member.rut) === normalizeRut(fixture.rut),
      )
    )
      members.push(structuredClone(fixture));
  const reservations = parsed.reservations.map((reservation) => ({
    ...reservation,
    branch:
      reservation.branch ??
      members.find((member) => member.id === reservation.memberId)?.branch,
    visits:
      reservation.visits ??
      (reservation.entry
        ? [
            {
              id: `legacy-${reservation.id}`,
              entryAt: reservation.entry,
              exitAt: reservation.exit,
              operatorRole: "admin" as const,
              document: "Carnet" as const,
            },
          ]
        : []),
  }));
  return reconcileAttendance({
    ...parsed,
    members,
    reservations,
    groups: parsed.groups ?? [],
    alerts: parsed.alerts ?? [],
  });
}
export function loadDemo(): DemoState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return structuredClone(initialState);
    const parsed = JSON.parse(raw) as DemoState;
    const statuses = [
      "Reservada",
      "En sala",
      "Fuera de sala",
      "Completada",
      "Cancelada",
      "No asistió",
    ];
    if (
      !Array.isArray(parsed.members) ||
      !Array.isArray(parsed.reservations) ||
      !parsed.members.every(
        (member) =>
          member &&
          typeof member.id === "string" &&
          typeof member.name === "string" &&
          typeof member.email === "string" &&
          typeof member.category === "string" &&
          typeof member.enabled === "boolean",
      ) ||
      !parsed.members.some((member) => member.id === DEMO_MEMBER) ||
      !parsed.reservations.every(
        (reservation) =>
          reservation &&
          typeof reservation.id === "string" &&
          typeof reservation.memberId === "string" &&
          statuses.includes(reservation.status) &&
          reservation.slot &&
          typeof reservation.slot.id === "string" &&
          /^\d{4}-\d{2}-\d{2}$/.test(reservation.slot.date) &&
          typeof reservation.slot.start === "string" &&
          typeof reservation.slot.end === "string" &&
          Number.isFinite(reservation.slot.capacity) &&
          Number.isFinite(reservation.slot.occupied),
      )
    )
      return structuredClone(initialState);
    return migrateState(parsed);
  } catch {
    return structuredClone(initialState);
  }
}

export type Role = "student" | "admin";
export type ReservationStatus =
  "Reservada" | "En sala" | "Completada" | "Cancelada";
export interface Member {
  id: string;
  name: string;
  email: string;
  category: string;
  enabled: boolean;
}
export interface Slot {
  id: string;
  date: string;
  start: string;
  end: string;
  capacity: number;
  occupied: number;
}
export interface Reservation {
  id: string;
  memberId: string;
  slot: Slot;
  status: ReservationStatus;
  entry?: string;
  exit?: string;
}
export interface DemoState {
  members: Member[];
  reservations: Reservation[];
}

export const STORAGE_KEY = "ucn-gym:demo:v1";
export const DEMO_MEMBER = "student-1";
export const initialState: DemoState = {
  members: [
    {
      id: DEMO_MEMBER,
      name: "Alex Martínez",
      email: "alex@example.invalid",
      category: "Estudiante",
      enabled: true,
    },
    {
      id: "student-2",
      name: "Camila Rojas",
      email: "camila@example.invalid",
      category: "Estudiante",
      enabled: true,
    },
    {
      id: "staff-1",
      name: "Daniel Soto",
      email: "daniel@example.invalid",
      category: "Funcionario",
      enabled: false,
    },
  ],
  reservations: [],
};

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
    ["11:00", "12:30", 20],
    ["14:00", "15:30", 9],
    ["15:30", "17:00", 16],
    ["17:00", "18:30", 4],
  ].map(([start, end, occupied], i) => ({
    id: `${date}-${i}`,
    date,
    start: String(start),
    end: String(end),
    capacity: 20,
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

export function remaining(slot: Slot, state: DemoState): number {
  return Math.max(
    0,
    slot.capacity -
      slot.occupied -
      state.reservations.filter(
        (r) =>
          r.slot.id === slot.id && ["Reservada", "En sala"].includes(r.status),
      ).length,
  );
}

export function reserve(
  state: DemoState,
  memberId: string,
  slot: Slot,
): DemoState {
  if (!state.members.find((m) => m.id === memberId)?.enabled)
    throw new Error("La cuenta no está habilitada.");
  if (remaining(slot, state) < 1)
    throw new Error("Este bloque no tiene cupos.");
  if (
    state.reservations.some(
      (r) =>
        r.memberId === memberId &&
        r.slot.id === slot.id &&
        r.status !== "Cancelada",
    )
  )
    throw new Error("Ya tienes una reserva en este bloque.");
  return {
    ...state,
    reservations: [
      ...state.reservations,
      { id: crypto.randomUUID(), memberId, slot, status: "Reservada" },
    ],
  };
}

export function cancel(state: DemoState, id: string): DemoState {
  const reservation = state.reservations.find((r) => r.id === id);
  if (!reservation || reservation.status !== "Reservada")
    throw new Error("Solo puedes cancelar una reserva pendiente.");
  return {
    ...state,
    reservations: state.reservations.map((r) =>
      r.id === id ? { ...r, status: "Cancelada" } : r,
    ),
  };
}

export function ticket(id: string): string {
  return `UCN:${id}`;
}

export function access(
  state: DemoState,
  code: string,
  action: "entry" | "exit",
): DemoState {
  const prefix = code.trim().startsWith("UCN-DEMO:") ? "UCN-DEMO:" : "UCN:";
  if (!code.trim().startsWith(prefix)) throw new Error("Código QR no válido.");
  const id = code.trim().slice(prefix.length);
  const reservation = state.reservations.find((r) => r.id === id);
  if (!reservation) throw new Error("No encontramos la reserva.");
  if (
    action === "entry" &&
    !state.members.find((m) => m.id === reservation.memberId)?.enabled
  )
    throw new Error("La cuenta no está habilitada.");
  if (action === "entry" && reservation.status !== "Reservada")
    throw new Error("La reserva no está disponible para ingresar.");
  if (action === "exit" && reservation.status !== "En sala")
    throw new Error("No hay una entrada abierta para esta reserva.");
  // Simulation only: time windows and eligibility must be validated by a production API.
  return {
    ...state,
    reservations: state.reservations.map((r) =>
      r.id !== id
        ? r
        : {
            ...r,
            status: action === "entry" ? "En sala" : "Completada",
            ...(action === "entry"
              ? { entry: new Date().toISOString() }
              : { exit: new Date().toISOString() }),
          },
    ),
  };
}

export function loadDemo(): DemoState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return structuredClone(initialState);
    const parsed = JSON.parse(raw) as DemoState;
    const statuses = ["Reservada", "En sala", "Completada", "Cancelada"];
    if (
      !Array.isArray(parsed.members) ||
      !Array.isArray(parsed.reservations) ||
      !parsed.members.every(
        (m) =>
          typeof m.id === "string" &&
          typeof m.name === "string" &&
          typeof m.email === "string" &&
          typeof m.category === "string" &&
          typeof m.enabled === "boolean",
      ) ||
      !parsed.members.some((m) => m.id === DEMO_MEMBER) ||
      !parsed.reservations.every(
        (r) =>
          typeof r.id === "string" &&
          typeof r.memberId === "string" &&
          statuses.includes(r.status) &&
          r.slot &&
          typeof r.slot.id === "string" &&
          /^\d{4}-\d{2}-\d{2}$/.test(r.slot.date) &&
          typeof r.slot.start === "string" &&
          typeof r.slot.end === "string" &&
          Number.isFinite(r.slot.capacity) &&
          Number.isFinite(r.slot.occupied),
      )
    )
      return structuredClone(initialState);
    return parsed;
  } catch {
    return structuredClone(initialState);
  }
}

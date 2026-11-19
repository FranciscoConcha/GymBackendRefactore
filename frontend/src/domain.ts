import type {
  AlertResponse,
  BranchResponse,
  ReservationResponse,
  ScheduleResponse,
  UserResponse,
  VisitResponse,
} from "./api";
export type Role = "student" | "admin" | "reception";
export type { Category, ReservationStatus } from "./api";
import type { Category, ReservationStatus } from "./api";
export const CATEGORIES: Category[] = [
  "Deportista",
  "Estudiante",
  "Funcionario",
  "Externo",
];
export interface Member {
  id: string;
  name: string;
  rut: string;
  email: string;
  category: string;
  branch?: string;
  branchId?: number;
  enabled: boolean;
  accountStatus?: string;
  role?: UserResponse["role"];
  username?: string;
  firstName?: string;
  lastName?: string;
}
export interface Slot {
  id: string;
  date: string;
  start: string;
  end: string;
  capacity: number;
  occupied: number;
  groupSeats: number;
  individualReservations: number;
}
export interface Reservation {
  id: string;
  memberId: string;
  userName: string;
  slot: Slot;
  status: ReservationStatus;
  createdAt: string;
  branch?: string;
  entry?: string;
  exit?: string;
  visits?: VisitResponse[];
}
export interface ViewState {
  members: Member[];
  reservations: Reservation[];
  alerts: AlertResponse[];
  branches: BranchResponse[];
}
export const emptyState: ViewState = {
  members: [],
  reservations: [],
  alerts: [],
  branches: [],
};
export function memberFrom(user: UserResponse): Member {
  return {
    id: String(user.id),
    name: user.fullName,
    firstName: user.firstName,
    lastName: user.lastName,
    rut: user.rut,
    email: user.email,
    category: user.category ?? "",
    branch: user.sportBranch?.name,
    branchId: user.sportBranch?.id,
    enabled: user.enabled,
    accountStatus: user.status,
    role: user.role,
    username: user.username,
  };
}
export function slotFrom(slot: ScheduleResponse): Slot {
  return {
    id: String(slot.id),
    date: slot.date,
    start: slot.startTime,
    end: slot.endTime,
    capacity: slot.capacity,
    occupied: slot.reservedCount,
    groupSeats: slot.groupSeats,
    individualReservations: slot.individualReservations,
  };
}
export function reservationFrom(
  r: ReservationResponse,
  branches: BranchResponse[] = [],
): Reservation {
  return {
    id: String(r.id),
    memberId: String(r.userId),
    userName: r.userName,
    status: r.status,
    createdAt: r.createdAt,
    branch: branches.find((b) => b.id === r.sportBranchId)?.name,
    slot: {
      id: String(r.scheduleSlotId),
      date: r.date,
      start: r.startTime,
      end: r.endTime,
      capacity: r.capacity,
      occupied: 0,
      groupSeats: 0,
      individualReservations: 0,
    },
  };
}
export function rutCheckDigit(body: string): string {
  let total = 0,
    factor = 2;
  for (let i = body.length - 1; i >= 0; i--) {
    total += Number(body[i]) * factor;
    factor = factor === 7 ? 2 : factor + 1;
  }
  const result = 11 - (total % 11);
  return result === 11 ? "0" : result === 10 ? "K" : String(result);
}
export function normalizeRut(value: string) {
  return value.replace(/[.\s-]/g, "").toUpperCase();
}
export function isValidRut(value: string) {
  const rut = normalizeRut(value);
  return (
    /^\d{7,8}[0-9K]$/.test(rut) &&
    rutCheckDigit(rut.slice(0, -1)) === rut.at(-1)
  );
}
export function formatRut(value: string) {
  const rut = normalizeRut(value);
  return rut
    ? `${rut.slice(0, -1).replace(/\B(?=(\d{3})+(?!\d))/g, ".")}-${rut.at(-1)}`
    : "";
}
export function localDate(date = new Date()) {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "America/Santiago",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(date);
  return ["year", "month", "day"]
    .map((k) => parts.find((p) => p.type === k)!.value)
    .join("-");
}
export function getDays(today = new Date()): string[] {
  const start = localDate(today);
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(`${start}T12:00:00Z`);
    d.setUTCDate(d.getUTCDate() + i);
    return localDate(d);
  });
}
export function shortDate(value: string, options?: Intl.DateTimeFormatOptions) {
  return new Intl.DateTimeFormat(
    "es-CL",
    options ?? { weekday: "long", day: "numeric", month: "long" },
  ).format(new Date(`${value}T12:00:00`));
}
// Schedule dates and times belong to Santiago regardless of the browser's timezone.
export function hasStarted(slot: Slot, now = new Date()) {
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: "America/Santiago",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).format(now);
  return `${slot.date}T${slot.start}` <= `${localDate(now)}T${parts}`;
}
export function remaining(slot: Slot) {
  return Math.max(0, slot.capacity - slot.occupied);
}
export function qrValidity(from: string, until: string, now = Date.now()) {
  return now < Date.parse(from)
    ? "El código QR todavía no está vigente."
    : now >= Date.parse(until)
      ? "El código QR ya venció."
      : "Código QR vigente para este bloque.";
}

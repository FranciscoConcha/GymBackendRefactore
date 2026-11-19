export type Role = "student" | "admin";
export interface Member {
  id: number;
  name: string;
  email: string;
  category: string;
  enabled: boolean;
}
export interface Slot {
  id: number;
  date: string;
  start: string;
  end: string;
  capacity: number;
  occupied: number;
  zone: string;
}
export interface Reservation {
  id: number;
  memberId: number;
  userName: string;
  slot: Slot;
  status: import("./api").ReservationResponse["status"];
}
export interface AppData {
  members: Member[];
  reservations: Reservation[];
  slots: Slot[];
}
export function remaining(slot: Slot): number {
  return Math.max(0, slot.capacity - slot.occupied);
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

export function shortDate(
  value: string,
  options?: Intl.DateTimeFormatOptions,
): string {
  return new Intl.DateTimeFormat(
    "es-CL",
    options ?? { weekday: "long", day: "numeric", month: "long" },
  ).format(new Date(`${value}T12:00:00`));
}

/** Orientativo para la interfaz: el servidor decide con la hora de Santiago. */
export function hasStarted(slot: Slot, now = new Date()): boolean {
  const today = localDate(now);
  if (slot.date !== today) return slot.date < today;
  const current = `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;
  return current >= slot.start;
}

export function durationMinutes(slot: Slot): number {
  const minutes = (value: string) => {
    const [h, m] = value.split(":").map(Number);
    return h * 60 + m;
  };
  return Math.max(0, minutes(slot.end) - minutes(slot.start));
}

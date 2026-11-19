import { localDate, type AppData } from "./domain";

export type Period = 1 | 7 | 30;

export function dashboardMetrics(
  state: AppData,
  period: Period,
  now = new Date(),
) {
  const days = Array.from({ length: period }, (_, index) => {
    const date = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    date.setDate(date.getDate() - period + index + 1);
    return localDate(date);
  });
  const records = state.reservations.filter((r) => days.includes(r.slot.date));
  const valid = records.filter((r) => r.status !== "Cancelado");
  const arrivals = valid.filter((r) => r.status === "Asistió");
  const cancelled = records.filter((r) => r.status === "Cancelado");
  const starts = [...new Set(valid.map((r) => r.slot.start))].sort();
  return {
    days,
    records,
    valid,
    arrivals,
    cancelled,
    enabled: state.members.filter((m) => m.enabled).length,
    pending: records.filter((r) => r.status === "Reservado").length,
    attendanceRate: valid.length
      ? Math.round((arrivals.length / valid.length) * 100)
      : null,
    cancellationRate: records.length
      ? Math.round((cancelled.length / records.length) * 100)
      : null,
    daily: days.map((date) => ({
      date,
      total: records.filter((r) => r.slot.date === date).length,
      cancelled: cancelled.filter((r) => r.slot.date === date).length,
    })),
    demand: starts
      .map((start) => ({
        start,
        count: valid.filter((r) => r.slot.start === start).length,
      }))
      .sort((a, b) => b.count - a.count || a.start.localeCompare(b.start)),
  };
}

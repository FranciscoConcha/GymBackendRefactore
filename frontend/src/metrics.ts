import {
  hasAttended,
  presentReservations,
  localDate,
  type DemoState,
} from "./domain";

export type Period = 1 | 7 | 30;
export { ROOM_CAPACITY } from "./domain";

export function dashboardMetrics(
  state: DemoState,
  period: Period,
  now = new Date(),
  branch = "",
) {
  const days = Array.from({ length: period }, (_, index) => {
    const date = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    date.setDate(date.getDate() - period + index + 1);
    return localDate(date);
  });
  const records = state.reservations.filter(
    (r) =>
      days.includes(r.slot.date) &&
      (!branch ||
        (r.branch ?? state.members.find((m) => m.id === r.memberId)?.branch) ===
          branch),
  );
  const valid = records.filter((r) => r.status !== "Cancelada");
  const arrivals = valid.filter(hasAttended);
  const present = presentReservations(state);
  const cancelled = records.filter((r) => r.status === "Cancelada");
  const durations = records.flatMap((r) => {
    if (r.visits?.length) {
      const closed = r.visits.filter(
        (visit) =>
          visit.exitAt &&
          Number.isFinite(Date.parse(visit.exitAt)) &&
          Date.parse(visit.exitAt) >= Date.parse(visit.entryAt),
      );
      return closed.length
        ? [
            closed.reduce(
              (sum, visit) =>
                sum +
                (Date.parse(visit.exitAt!) - Date.parse(visit.entryAt)) / 60000,
              0,
            ),
          ]
        : [];
    }
    if (!r.entry || !r.exit || r.status !== "Completada") return [];
    const minutes = (Date.parse(r.exit) - Date.parse(r.entry)) / 60000;
    return Number.isFinite(minutes) && minutes >= 0 ? [minutes] : [];
  });
  const starts = [...new Set(valid.map((r) => r.slot.start))].sort();
  return {
    days,
    records,
    valid,
    arrivals,
    present,
    cancelled,
    enabled: state.members.filter(
      (m) => m.enabled && (!branch || m.branch === branch),
    ).length,
    memberCount: state.members.filter((m) => !branch || m.branch === branch)
      .length,
    noShows: records.filter((r) => r.status === "No asistió").length,
    groupParticipants: records.filter(
      (r) => r.groupId && r.status !== "Cancelada",
    ).length,
    visitCount: records.reduce(
      (sum, r) => sum + (r.visits?.length ?? (r.entry ? 1 : 0)),
      0,
    ),
    pending: records.filter((r) => r.status === "Reservada").length,
    attendanceRate: valid.length
      ? Math.round((arrivals.length / valid.length) * 100)
      : null,
    cancellationRate: records.length
      ? Math.round((cancelled.length / records.length) * 100)
      : null,
    averageMinutes: durations.length
      ? Math.round(
          durations.reduce((sum, value) => sum + value, 0) / durations.length,
        )
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

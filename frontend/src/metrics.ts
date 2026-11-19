import type { MetricsResponse } from "./api";
export function attendanceRate(
  metrics: Pick<MetricsResponse, "reservations">,
): number | null {
  const eligible = metrics.reservations.total - metrics.reservations.cancelled;
  return eligible > 0
    ? Math.round((metrics.reservations.attended / eligible) * 100)
    : null;
}

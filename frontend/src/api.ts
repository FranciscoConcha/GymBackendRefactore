export interface UserResponse {
  id: number;
  rut: string;
  name: string;
  email: string;
  username: string;
  userType: string;
  role: "Admin" | "Cliente";
  enabled: boolean;
}
export interface ScheduleResponse {
  id: number;
  day: string;
  startTime: string;
  endTime: string;
  zone: string;
  capacity: number;
  reservedCount: number;
  date: string;
}
export interface ReservationResponse {
  id: number;
  userId: number;
  userName: string;
  scheduleSlotId: number;
  date: string;
  day: string;
  startTime: string;
  endTime: string;
  zone: string;
  capacity: number;
  status: "Reservado" | "Cancelado" | "Asistió";
  createdAt: string;
}
export interface QrResponse {
  rut: string;
  name: string;
  date: string;
  token: string;
  validFrom: string;
  validUntil: string;
}
export interface RegisterRequest {
  rut: string;
  name: string;
  email: string;
  password: string;
  userType: "Estudiante" | "Administrador";
}
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}
const unauthorized = new Set<() => void>();
export function onUnauthorized(handler: () => void) {
  unauthorized.add(handler);
  return () => {
    unauthorized.delete(handler);
  };
}
export async function request<T>(
  path: string,
  options: { method?: string; body?: unknown; sessionCheck?: boolean } = {},
): Promise<T> {
  const method = (options.method ?? "GET").toUpperCase();
  const headers: Record<string, string> = { Accept: "application/json" };
  if (["POST", "PUT", "PATCH", "DELETE"].includes(method))
    headers["X-Gym-Csrf"] = "1";
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  const response = await fetch(`/api/v1${path}`, {
    method,
    headers,
    credentials: "same-origin",
    ...(options.body === undefined
      ? {}
      : { body: JSON.stringify(options.body) }),
  });
  const data = await response.json().catch(() => null);
  if (!response.ok) {
    if (response.status === 401 && !options.sessionCheck)
      unauthorized.forEach((handler) => handler());
    throw new ApiError(
      response.status,
      data?.message ||
        data?.detail ||
        data?.title ||
        `Error HTTP ${response.status}`,
    );
  }
  return data as T;
}
export const api = {
  me: () => request<UserResponse>("/auth/me", { sessionCheck: true }),
  login: (admin: boolean, identifier: string, password: string) =>
    request<{ user: UserResponse; message: string; expiresAt: string }>(
      admin ? "/auth/admin-login" : "/auth/login",
      {
        method: "POST",
        body: admin
          ? { username: identifier, password }
          : { rut: identifier, password },
        sessionCheck: true,
      },
    ),
  logout: () => request("/auth/logout", { method: "POST" }),
  schedules: (date: string) =>
    request<ScheduleResponse[]>(`/schedules?date=${encodeURIComponent(date)}`),
  reservations: (user: UserResponse) =>
    request<ReservationResponse[]>(
      user.role === "Admin" ? "/reservations" : `/reservations/user/${user.id}`,
    ),
  reserve: (scheduleSlotId: number, date: string) =>
    request("/reservations", {
      method: "POST",
      body: { scheduleSlotId, date },
    }),
  cancel: (id: number) => request(`/reservations/${id}`, { method: "DELETE" }),
  qr: () => request<QrResponse>("/qr/my-code"),
  access: (token: string, action: "entry" | "exit") =>
    request<{ message: string; attendance: unknown }>(`/attendance/${action}`, {
      method: "POST",
      body: { token },
    }),
  users: () => request<UserResponse[]>("/auth/users"),
  status: (id: number, enabled: boolean) =>
    request(`/auth/users/${id}/status`, { method: "PATCH", body: { enabled } }),
  register: (body: RegisterRequest) =>
    request("/auth/register", { method: "POST", body }),
};

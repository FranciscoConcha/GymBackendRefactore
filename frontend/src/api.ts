export type Role = "Admin" | "Recepcionista" | "Usuario";
export type Category = "Deportista" | "Estudiante" | "Funcionario" | "Externo";
export interface BranchResponse {
  id: number;
  name: string;
  enabled: boolean;
}
export interface UserResponse {
  id: number;
  rut: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  username: string;
  role: Role;
  category: Category | null;
  sportBranch: Pick<BranchResponse, "id" | "name"> | null;
  enabled: boolean;
  status: "Pendiente" | "Activa" | "Deshabilitada";
  mustChangePassword: boolean;
  createdAt: string;
  updatedAt: string;
}
export interface ScheduleResponse {
  id: number;
  day: string;
  startTime: string;
  endTime: string;
  zone: string;
  capacity: number;
  reservedCount: number;
  groupSeats: number;
  individualReservations: number;
  date: string;
}
export type ReservationStatus =
  "Reservado" | "Cancelado" | "Asistió" | "Inasistencia";
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
  status: ReservationStatus;
  createdAt: string;
  category: Category | null;
  sportBranchId: number | null;
}
export interface AccessUser {
  id?: number;
  firstName: string;
  lastName: string;
  fullName: string;
  rut: string;
  category: Category | null;
  sportBranch: Pick<BranchResponse, "id" | "name"> | null;
}
export interface AccessSession {
  kind: "Reserva" | "Grupo";
  reservationId: number | null;
  groupBookingId: number | null;
  date: string;
  startTime: string;
  endTime: string;
}
export interface QrResponse {
  token: string;
  validFrom: string;
  validUntil: string;
  user: AccessUser;
  session: AccessSession;
}
export interface Occupancy {
  current: number;
  max: number;
}
export interface AccessValidation {
  user: AccessUser;
  session: AccessSession | null;
  inside: boolean;
  canEnter: boolean;
  reason?: string;
  occupancy: Occupancy;
}
export interface CurrentAccess {
  occupancy: Occupancy;
  people: {
    userId: number;
    fullName: string;
    rut: string;
    category: Category | null;
    entryAt: string;
    sessionEndTime: string;
  }[];
}
export type DocumentType =
  "CarnetIdentidad" | "TNE" | "CredencialUniversitaria";
export interface VisitResponse {
  id: number;
  userId: number;
  fullName: string;
  rut: string;
  reservationId: number | null;
  groupBookingId: number | null;
  date: string;
  entryAt: string;
  exitAt: string | null;
  stayMinutes: number | null;
  documentType: DocumentType;
  entryOperator: string;
  exitOperator: string | null;
}
export interface GroupBookingResponse {
  id: number;
  sportBranch: Pick<BranchResponse, "id" | "name">;
  scheduleSlotId: number;
  date: string;
  startTime: string;
  endTime: string;
  mode: "Cupos" | "TotalDisponible";
  seats: number;
  usedSeats: number;
  status: "Activa" | "Cancelada";
  createdBy: string;
  createdAt: string;
}
export interface GroupBookingRequest {
  sportBranchId: number;
  scheduleSlotId: number;
  date: string;
  mode: "Cupos" | "TotalDisponible";
  seats?: number;
}
export interface AlertResponse {
  id: number;
  reservationId: number;
  date: string;
  startTime: string;
  endTime: string;
  createdAt: string;
  acknowledgedAt: string | null;
}
export interface ReportFilters {
  from?: string;
  to?: string;
  branchId?: number;
  category?: string;
}
export interface MetricsResponse {
  period: { from: string; to: string };
  filters: { branchId: number | null; category: string | null };
  occupancy: Occupancy;
  reservations: {
    total: number;
    attended: number;
    cancelled: number;
    noShow: number;
  };
  groupBookings: { count: number; seats: number; usedSeats: number };
  uniqueVisitors: number;
  visits: number;
  averageStayMinutes: number | null;
  demandByHour: { startTime: string; reservations: number }[];
  demandByBranch: { branchId: number; name: string; reservations: number }[];
  enabledUsersByCategory: { category: string; count: number }[];
}
export interface ImportRow {
  row: number;
  rut: string;
  firstName: string;
  lastName: string;
  email: string;
  category: string;
  branch: string;
  action: "Crear" | "Omitir" | "Error";
  messages: string[];
}
export interface ImportPreview {
  id: number;
  expiresAt: string;
  summary: { create: number; skip: number; error: number };
  rows: ImportRow[];
}
export interface ImportResult {
  created: number;
  skipped: number;
  failed: number;
  rows: ImportRow[];
}
export interface RegisterRequest {
  rut: string;
  firstName: string;
  lastName: string;
  email: string;
  password?: string;
  role: Role;
  category: Category | null;
  sportBranchId: number | null;
  username?: string;
}
export type UpdateUserRequest = Partial<
  Omit<RegisterRequest, "rut" | "password">
>;
export interface Message {
  message: string;
}
export interface AuthResponse extends Message {
  user: UserResponse;
  expiresAt: string;
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
const passwordRequired = new Set<() => void>();
export function onUnauthorized(handler: () => void) {
  unauthorized.add(handler);
  return () => {
    unauthorized.delete(handler);
  };
}
export function onPasswordRequired(handler: () => void) {
  passwordRequired.add(handler);
  return () => {
    passwordRequired.delete(handler);
  };
}
interface RequestOptions {
  method?: string;
  body?: unknown;
  sessionCheck?: boolean;
  idempotencyKey?: string;
  blob?: boolean;
}
async function responseFor(path: string, options: RequestOptions) {
  const method = (options.method ?? "GET").toUpperCase();
  const headers: Record<string, string> = {
    Accept: options.blob
      ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
      : "application/json",
  };
  if (["POST", "PUT", "PATCH", "DELETE"].includes(method))
    headers["X-Gym-Csrf"] = "1";
  if (options.idempotencyKey)
    headers["Idempotency-Key"] = options.idempotencyKey;
  const multipart = options.body instanceof FormData;
  if (options.body !== undefined && !multipart)
    headers["Content-Type"] = "application/json";
  const response = await fetch(`/api/v1${path}`, {
    method,
    headers,
    credentials: "same-origin",
    ...(options.body === undefined
      ? {}
      : {
          body: multipart
            ? (options.body as FormData)
            : JSON.stringify(options.body),
        }),
  });
  if (!response.ok) {
    const data = await response.json().catch(() => null);
    const message =
      data?.message ||
      data?.detail ||
      data?.title ||
      `Error HTTP ${response.status}`;
    if (response.status === 401 && !options.sessionCheck)
      unauthorized.forEach((h) => h());
    if (
      response.status === 403 &&
      message.includes("Debe cambiar su contraseña temporal")
    )
      passwordRequired.forEach((h) => h());
    throw new ApiError(response.status, message);
  }
  return response;
}
export async function request<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const response = await responseFor(path, options);
  return response.status === 204
    ? (undefined as T)
    : (response.json().catch(() => null) as Promise<T>);
}
export function query(filters: object = {}) {
  const params = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "")
      params.set(key, String(value));
  });
  return params.size ? `?${params}` : "";
}
const post = <T = Message>(
  path: string,
  body?: unknown,
  idempotencyKey?: string,
) => request<T>(path, { method: "POST", body, idempotencyKey });
export const api = {
  me: () => request<UserResponse>("/auth/me", { sessionCheck: true }),
  login: (staff: boolean, identifier: string, password: string) =>
    request<AuthResponse>(staff ? "/auth/admin-login" : "/auth/login", {
      method: "POST",
      body: staff
        ? { username: identifier, password }
        : { rut: identifier, password },
      sessionCheck: true,
    }),
  logout: () => post("/auth/logout"),
  changePassword: (currentPassword: string, newPassword: string) =>
    post<AuthResponse>("/auth/change-password", {
      currentPassword,
      newPassword,
    }),
  activate: (token: string, newPassword: string) =>
    post("/auth/activate", { token, newPassword }),
  recover: (email: string) => post("/auth/password-resets", { email }),
  reset: (token: string, newPassword: string) =>
    post("/auth/password-resets/confirm", { token, newPassword }),
  schedules: (date: string) =>
    request<ScheduleResponse[]>(`/schedules${query({ date })}`),
  reservations: (user: UserResponse) =>
    request<ReservationResponse[]>(
      user.role === "Admin" ? "/reservations" : `/reservations/user/${user.id}`,
    ),
  reserve: (scheduleSlotId: number, date: string, key: string) =>
    post<Message & { reservation: ReservationResponse }>(
      "/reservations",
      { scheduleSlotId, date },
      key,
    ),
  cancel: (id: number) =>
    request<Message & { reservation: ReservationResponse }>(
      `/reservations/${id}`,
      { method: "DELETE" },
    ),
  qr: (id: number, group = false) =>
    request<QrResponse>(
      `/me/${group ? "group-bookings" : "reservations"}/${id}/qr`,
    ),
  users: (
    filters: {
      search?: string;
      role?: string;
      category?: string;
      branchId?: number;
      enabled?: boolean;
    } = {},
  ) => request<UserResponse[]>(`/auth/users${query(filters)}`),
  status: (id: number, enabled: boolean) =>
    request<Message>(`/auth/users/${id}/status`, {
      method: "PATCH",
      body: { enabled },
    }),
  register: (body: RegisterRequest) =>
    post<Message & { user: UserResponse }>("/auth/register", body),
  updateUser: (id: number, body: UpdateUserRequest) =>
    request<Message>(`/auth/users/${id}`, { method: "PATCH", body }),
  invitation: (id: number) => post(`/auth/users/${id}/invitation`),
  passwordReset: (id: number, temporaryPassword: string) =>
    post(`/auth/users/${id}/password-reset`, { temporaryPassword }),
  branches: () => request<BranchResponse[]>("/branches"),
  createBranch: (name: string) =>
    post<Message & { branch: BranchResponse }>("/branches", { name }),
  updateBranch: (id: number, body: { name?: string; enabled?: boolean }) =>
    request<Message>(`/branches/${id}`, { method: "PATCH", body }),
  groups: (filters: ReportFilters = {}) =>
    request<GroupBookingResponse[]>(`/group-bookings${query(filters)}`),
  myGroups: () => request<GroupBookingResponse[]>("/me/group-bookings"),
  createGroup: (body: GroupBookingRequest, key: string) =>
    post<Message & { groupBooking: GroupBookingResponse }>(
      "/group-bookings",
      body,
      key,
    ),
  cancelGroup: (id: number) => post(`/group-bookings/${id}/cancel`),
  validate: (token: string) =>
    post<AccessValidation>("/access/validate", { token }),
  entry: (token: string, documentType: DocumentType, key: string) =>
    post<Message & { visit: VisitResponse }>(
      "/access/entry",
      { token, documentType },
      key,
    ),
  exit: (userId: number) =>
    post<Message & { visit: VisitResponse }>("/access/exit", { userId }),
  current: () => request<CurrentAccess>("/access/current"),
  visits: (filters: ReportFilters & { userId?: number } = {}) =>
    request<VisitResponse[]>(`/visits${query(filters)}`),
  myVisits: () => request<VisitResponse[]>("/me/visits"),
  alerts: () => request<AlertResponse[]>("/me/alerts"),
  acknowledge: (id: number) => post(`/me/alerts/${id}/acknowledge`),
  adminAlerts: (filters: ReportFilters = {}) =>
    request<AlertResponse[]>(`/admin/alerts${query(filters)}`),
  metrics: (filters: ReportFilters = {}) =>
    request<MetricsResponse>(`/admin/metrics${query(filters)}`),
  previewImport: (file: File) => {
    const body = new FormData();
    body.append("file", file);
    return post<ImportPreview>("/admin/user-imports", body);
  },
  confirmImport: (id: number, key: string) =>
    post<ImportResult>(`/admin/user-imports/${id}/confirm`, undefined, key),
  export: async (filters: ReportFilters) => {
    const response = await responseFor("/admin/exports", {
      method: "POST",
      body: filters,
      blob: true,
    });
    const disposition = response.headers.get("Content-Disposition") ?? "";
    const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
    const plain = /filename="([^"]+)"|filename=([^;]+)/i.exec(disposition);
    let filename = plain?.[1] ?? plain?.[2]?.trim() ?? "gym.xlsx";
    if (encoded) {
      try {
        filename = decodeURIComponent(encoded);
      } catch {
        /* use plain filename */
      }
    }
    return {
      blob: await response.blob(),
      filename: filename.split(/[\\/]/).at(-1) || "gym.xlsx",
    };
  },
};

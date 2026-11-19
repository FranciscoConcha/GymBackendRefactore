import { expect, type Page } from "@playwright/test";
import type {
  AlertResponse,
  BranchResponse,
  CurrentAccess,
  GroupBookingResponse,
  ImportPreview,
  MetricsResponse,
  ReservationResponse,
  Role,
  UserResponse,
  VisitResponse,
} from "../src/api";
export interface MockOptions {
  role?: Role;
  mustChangePassword?: boolean;
  alerts?: boolean;
}
export async function mockApi(page: Page, options: MockOptions = {}) {
  const morning = new Date();
  morning.setHours(6, 0, 0, 0);
  await page.clock.setFixedTime(morning);
  const date = new Intl.DateTimeFormat("en-CA", {
    timeZone: "America/Santiago",
  }).format(morning);
  const timestamp = morning.toISOString();
  const branches: BranchResponse[] = [
    { id: 1, name: "Fútbol", enabled: true },
    { id: 2, name: "Básquetbol", enabled: true },
  ];
  const student: UserResponse = {
    id: 1,
    rut: "12345678-5",
    firstName: "Alex",
    lastName: "Martínez",
    fullName: "Alex Martínez",
    email: "alex@example.invalid",
    username: "alex",
    role: "Usuario",
    category: "Deportista",
    sportBranch: { id: 1, name: "Fútbol" },
    enabled: true,
    status: "Activa",
    mustChangePassword: options.mustChangePassword ?? false,
    createdAt: timestamp,
    updatedAt: timestamp,
  };
  const admin: UserResponse = {
    ...student,
    id: 2,
    firstName: "Equipo",
    lastName: "UCN",
    fullName: "Equipo UCN",
    role: "Admin",
    category: null,
    sportBranch: null,
    username: "admin",
    mustChangePassword: false,
  };
  const reception: UserResponse = {
    ...admin,
    id: 3,
    role: "Recepcionista",
    username: "recepcion",
  };
  const users = [student, admin, reception];
  let session: UserResponse | null = options.role
    ? users.find((u) => u.role === options.role)!
    : null;
  const reservations: ReservationResponse[] = [];
  const groups: GroupBookingResponse[] = [];
  const visits: VisitResponse[] = [];
  const alerts: AlertResponse[] = options.alerts
    ? [
        {
          id: 1,
          reservationId: 90,
          date,
          startTime: "08:00",
          endTime: "09:30",
          createdAt: timestamp,
          acknowledgedAt: null,
        },
      ]
    : [];
  const calls: {
    path: string;
    method: string;
    body: Record<string, unknown>;
    headers: Record<string, string>;
    query: Record<string, string>;
  }[] = [];
  const csrfFailures: string[] = [];
  const unknown: string[] = [];
  const failures: { path: string; status: number; message: string }[] = [];
  const keys = new Map<string, { path: string; body: string; data: unknown }>();
  const confirmed = new Map<number, unknown>();
  const occupancy = () => ({
    current: visits.filter((v) => !v.exitAt).length,
    max: 30,
  });
  const current = (): CurrentAccess => ({
    occupancy: occupancy(),
    people: visits
      .filter((v) => !v.exitAt)
      .map((v) => ({
        userId: v.userId,
        fullName: v.fullName,
        rut: v.rut,
        category: "Deportista",
        entryAt: v.entryAt,
        sessionEndTime: "09:30",
      })),
  });
  const preview: ImportPreview = {
    id: 1,
    expiresAt: new Date(morning.getTime() + 3600000).toISOString(),
    summary: { create: 1, skip: 1, error: 1 },
    rows: [
      {
        row: 2,
        rut: "11111111-1",
        firstName: "Camila",
        lastName: "Rojas",
        email: "camila@example.invalid",
        category: "Estudiante",
        branch: "",
        action: "Crear",
        messages: [],
      },
      {
        row: 3,
        rut: student.rut,
        firstName: "Alex",
        lastName: "Martínez",
        email: student.email,
        category: "Deportista",
        branch: "Fútbol",
        action: "Omitir",
        messages: ["El usuario ya existe."],
      },
      {
        row: 4,
        rut: "inválido",
        firstName: "Error",
        lastName: "Fila",
        email: "error@example.invalid",
        category: "Estudiante",
        branch: "",
        action: "Error",
        messages: ["RUT inválido."],
      },
    ],
  };
  await page.route("**/api/v1/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url()),
      path = url.pathname.replace("/api/v1", ""),
      method = req.method(),
      headers = req.headers();
    const multipart = headers["content-type"]?.startsWith("multipart/");
    const body: Record<string, unknown> = multipart
      ? { multipart: req.postData() ?? "" }
      : (req.postDataJSON() ?? {});
    calls.push({
      path,
      method,
      body,
      headers,
      query: Object.fromEntries(url.searchParams),
    });
    const send = (data: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(data),
      });
    const error = (message: string, status = 400) => send({ message }, status);
    const mutation = ["POST", "PATCH", "PUT", "DELETE"].includes(method);
    if (mutation && headers["x-gym-csrf"] !== "1") {
      csrfFailures.push(path);
      return error("Falta CSRF.", 403);
    }
    const failIndex = failures.findIndex((f) => f.path === path);
    if (failIndex >= 0) {
      const fail = failures.splice(failIndex, 1)[0];
      return error(fail.message, fail.status);
    }
    const publicRoute = [
      "/auth/login",
      "/auth/admin-login",
      "/auth/logout",
      "/auth/activate",
      "/auth/password-resets",
      "/auth/password-resets/confirm",
    ].includes(path);
    if (!publicRoute && !session) return error("Sin sesión.", 401);
    if (
      session?.mustChangePassword &&
      !["/auth/me", "/auth/change-password", "/auth/logout"].includes(path)
    )
      return error(
        "Debe cambiar su contraseña temporal antes de continuar.",
        403,
      );
    const adminOnly =
      path.startsWith("/admin/") ||
      path.startsWith("/auth/users") ||
      path === "/auth/register" ||
      path === "/visits" ||
      path.startsWith("/group-bookings") ||
      (path.startsWith("/branches") && mutation) ||
      (path === "/reservations" && method === "GET");
    if (adminOnly && session?.role !== "Admin")
      return error("Sin permiso.", 403);
    if (path.startsWith("/access/") && session?.role === "Usuario")
      return error("Sin permiso.", 403);
    const idempotent =
      mutation &&
      (path === "/reservations" ||
        path === "/group-bookings" ||
        path === "/access/entry" ||
        /\/user-imports\/\d+\/confirm$/.test(path));
    const key = headers["idempotency-key"];
    if (path === "/group-bookings" && method === "POST" && !key)
      return error("Se requiere Idempotency-Key.");
    if (idempotent && key && keys.has(key)) {
      const old = keys.get(key)!;
      return old.path !== path || old.body !== JSON.stringify(body)
        ? error("La clave de idempotencia ya se usó con otra solicitud.", 422)
        : send(old.data);
    }
    const success = (data: unknown) => {
      if (idempotent && key)
        keys.set(key, { path, body: JSON.stringify(body), data });
      return send(data);
    };
    if (path === "/auth/me") return send(session);
    if (path === "/auth/login" || path === "/auth/admin-login") {
      session = path.endsWith("admin-login")
        ? body.username === "recepcion"
          ? reception
          : admin
        : student;
      return send({
        message: "Sesión iniciada.",
        user: session,
        expiresAt: new Date(morning.getTime() + 3600000).toISOString(),
      });
    }
    if (path === "/auth/change-password") {
      session = { ...session!, mustChangePassword: false };
      return send({
        message: "Contraseña actualizada.",
        user: session,
        expiresAt: timestamp,
      });
    }
    if (path === "/auth/logout") {
      session = null;
      return send({ message: "Sesión cerrada." });
    }
    if (path === "/auth/activate" || path === "/auth/password-resets/confirm") {
      if (!body.token || !body.newPassword)
        return error("El enlace no es válido o expiró.");
      return send({ message: "Contraseña actualizada." });
    }
    if (path === "/auth/password-resets")
      return send(
        {
          message:
            "Si el correo está registrado, recibirás un enlace para restablecer tu contraseña.",
        },
        202,
      );
    if (path === "/branches" && method === "GET") return send(branches);
    if (path === "/branches" && method === "POST") {
      const branch = {
        id: branches.length + 1,
        name: String(body.name),
        enabled: true,
      };
      branches.push(branch);
      return send({ message: "Rama creada.", branch });
    }
    if (/^\/branches\/\d+$/.test(path) && method === "PATCH") {
      Object.assign(
        branches.find((b) => b.id === Number(path.split("/").at(-1))),
        body,
      );
      return send({ message: "Rama actualizada." });
    }
    if (path === "/schedules") {
      const d = url.searchParams.get("date")!;
      return send([
        {
          id: 1,
          day: "Lunes",
          date: d,
          startTime: "08:00",
          endTime: "09:30",
          zone: "Sala de pesas",
          capacity: 30,
          individualReservations: reservations.filter(
            (r) => r.date === d && r.status !== "Cancelado",
          ).length,
          groupSeats: groups
            .filter((g) => g.date === d && g.status === "Activa")
            .reduce((n, g) => n + g.seats, 0),
          reservedCount:
            reservations.filter((r) => r.date === d && r.status !== "Cancelado")
              .length +
            groups
              .filter((g) => g.date === d && g.status === "Activa")
              .reduce((n, g) => n + g.seats, 0),
        },
      ]);
    }
    if (path === "/reservations" && method === "POST") {
      if (session?.role === "Recepcionista") return error("Sin permiso.", 403);
      const r: ReservationResponse = {
        id: reservations.length + 1,
        userId: session!.id,
        userName: session!.fullName,
        scheduleSlotId: Number(body.scheduleSlotId),
        date: String(body.date),
        day: "Lunes",
        startTime: "08:00",
        endTime: "09:30",
        zone: "Sala",
        capacity: 30,
        status: "Reservado",
        createdAt: timestamp,
        category: session!.category,
        sportBranchId: session!.sportBranch?.id ?? null,
      };
      reservations.push(r);
      return success({ message: "Reserva confirmada.", reservation: r });
    }
    if (
      method === "GET" &&
      (path === "/reservations" || /^\/reservations\/user\/\d+$/.test(path))
    )
      return send(
        path === "/reservations"
          ? reservations
          : reservations.filter(
              (r) => r.userId === Number(path.split("/").at(-1)),
            ),
      );
    if (method === "DELETE" && /^\/reservations\/\d+$/.test(path)) {
      const r = reservations.find(
        (r) => r.id === Number(path.split("/").at(-1)),
      )!;
      r.status = "Cancelado";
      return send({ message: "Reserva cancelada.", reservation: r });
    }
    if (/^\/me\/(reservations|group-bookings)\/\d+\/qr$/.test(path)) {
      const group = path.includes("group-bookings"),
        id = Number(path.split("/").at(-2));
      return send({
        token: `signed-${group ? "G" : "R"}-${id}`,
        validFrom: new Date(morning.getTime() + 105 * 60000).toISOString(),
        validUntil: new Date(morning.getTime() + 210 * 60000).toISOString(),
        user: student,
        session: {
          kind: group ? "Grupo" : "Reserva",
          reservationId: group ? null : id,
          groupBookingId: group ? id : null,
          date,
          startTime: "08:00",
          endTime: "09:30",
        },
      });
    }
    if (path === "/access/current") return send(current());
    if (path === "/access/validate")
      return send({
        user: student,
        session: {
          kind: "Reserva",
          reservationId: 1,
          groupBookingId: null,
          date,
          startTime: "08:00",
          endTime: "09:30",
        },
        inside: current().people.some((p) => p.userId === student.id),
        canEnter: true,
        occupancy: occupancy(),
      });
    if (path === "/access/entry") {
      if (!body.documentType) return error("Seleccione documento.");
      if (current().people.some((p) => p.userId === student.id))
        return error("La persona ya está dentro.", 409);
      const visit: VisitResponse = {
        id: visits.length + 1,
        userId: 1,
        fullName: student.fullName,
        rut: student.rut,
        reservationId: 1,
        groupBookingId: null,
        date,
        entryAt: timestamp,
        exitAt: null,
        stayMinutes: null,
        documentType: body.documentType as VisitResponse["documentType"],
        entryOperator: session!.fullName,
        exitOperator: null,
      };
      visits.push(visit);
      const r = reservations.find((r) => r.id === 1);
      if (r) r.status = "Asistió";
      return success({ message: "Entrada registrada.", visit });
    }
    if (path === "/access/exit") {
      const visit = visits.find((v) => v.userId === body.userId && !v.exitAt);
      if (!visit) return error("No hay una visita abierta.");
      visit.exitAt = timestamp;
      visit.exitOperator = session!.fullName;
      visit.stayMinutes = 0;
      return send({ message: "Salida registrada.", visit });
    }
    if (path === "/visits" || path === "/me/visits") return send(visits);
    if (path === "/me/alerts" || path === "/admin/alerts") return send(alerts);
    if (/^\/me\/alerts\/\d+\/acknowledge$/.test(path)) {
      alerts.find(
        (a) => a.id === Number(path.split("/").at(-2)),
      )!.acknowledgedAt = timestamp;
      return send({ message: "Alerta leída." });
    }
    if (path === "/group-bookings" && method === "POST") {
      const branch = branches.find((b) => b.id === body.sportBranchId)!;
      const g: GroupBookingResponse = {
        id: groups.length + 1,
        sportBranch: branch,
        scheduleSlotId: Number(body.scheduleSlotId),
        date: String(body.date),
        startTime: "08:00",
        endTime: "09:30",
        mode: body.mode as GroupBookingResponse["mode"],
        seats:
          body.mode === "Cupos" ? Number(body.seats) : 30 - reservations.length,
        usedSeats: 0,
        status: "Activa",
        createdBy: session!.fullName,
        createdAt: timestamp,
      };
      groups.push(g);
      return success({
        message: "Sesión de rama confirmada.",
        groupBooking: g,
      });
    }
    if (path === "/group-bookings" || path === "/me/group-bookings")
      return send(
        groups.filter(
          (g) =>
            path === "/group-bookings" ||
            g.sportBranch.id === session?.sportBranch?.id,
        ),
      );
    if (/^\/group-bookings\/\d+\/cancel$/.test(path)) {
      groups.find((g) => g.id === Number(path.split("/").at(-2)))!.status =
        "Cancelada";
      return send({ message: "Grupo cancelado." });
    }
    if (path === "/auth/users" && method === "GET")
      return send(
        users.filter(
          (u) =>
            (!url.searchParams.get("search") ||
              `${u.fullName} ${u.rut} ${u.email}`
                .toLowerCase()
                .includes(url.searchParams.get("search")!.toLowerCase())) &&
            (!url.searchParams.get("category") ||
              u.category === url.searchParams.get("category")) &&
            (!url.searchParams.get("branchId") ||
              u.sportBranch?.id === Number(url.searchParams.get("branchId"))) &&
            (!url.searchParams.has("enabled") ||
              u.enabled === (url.searchParams.get("enabled") === "true")),
        ),
      );
    if (path === "/auth/register") {
      const u: UserResponse = {
        ...student,
        ...body,
        id: users.length + 1,
        fullName: `${body.firstName} ${body.lastName}`,
        status: body.password ? "Activa" : "Pendiente",
        mustChangePassword: !!body.password,
      } as UserResponse;
      users.push(u);
      return send({ message: "Usuario creado.", user: u });
    }
    if (/^\/auth\/users\/\d+/.test(path)) {
      const id = Number(path.split("/")[3]);
      const u = users.find((u) => u.id === id)!;
      if (method === "PATCH") Object.assign(u, body);
      return send({ message: "Usuario actualizado.", user: u });
    }
    if (path === "/admin/user-imports") {
      if (!multipart || !String(body.multipart).includes('name="file"'))
        return error("Falta archivo.");
      return send(preview);
    }
    if (/^\/admin\/user-imports\/\d+\/confirm$/.test(path)) {
      if (!confirmed.has(1))
        confirmed.set(1, {
          created: 1,
          skipped: 1,
          failed: 1,
          rows: preview.rows,
        });
      return success(confirmed.get(1));
    }
    if (path === "/admin/exports")
      return route.fulfill({
        status: 200,
        contentType:
          "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers: {
          "Content-Disposition": "attachment; filename=UCN-reporte.xlsx",
        },
        body: Buffer.from("fixture-xlsx"),
      });
    if (path === "/admin/metrics") {
      const metrics: MetricsResponse = {
        period: {
          from: url.searchParams.get("from") ?? date,
          to: url.searchParams.get("to") ?? date,
        },
        filters: {
          branchId: url.searchParams.has("branchId")
            ? Number(url.searchParams.get("branchId"))
            : null,
          category: url.searchParams.get("category"),
        },
        occupancy: occupancy(),
        reservations: { total: 12, attended: 7, cancelled: 2, noShow: 3 },
        groupBookings: { count: 2, seats: 8, usedSeats: 3 },
        uniqueVisitors: 6,
        visits: 9,
        averageStayMinutes: 42,
        demandByHour: [
          { startTime: "08:00", reservations: 7 },
          { startTime: "09:30", reservations: 5 },
        ],
        demandByBranch: [{ branchId: 1, name: "Fútbol", reservations: 8 }],
        enabledUsersByCategory: [{ category: "Deportista", count: 15 }],
      };
      return send(metrics);
    }
    unknown.push(`${method} ${path}`);
    return error(`Endpoint no simulado: ${method} ${path}`, 404);
  });
  return {
    calls,
    csrfFailures,
    unknown,
    student,
    admin,
    reception,
    reservations,
    groups,
    visits,
    alerts,
    branches,
    preview,
    date,
    failNext: (path: string, status: number, message: string) =>
      failures.push({ path, status, message }),
    expire: () => {
      session = null;
    },
    requirePassword: () => {
      if (session) session.mustChangePassword = true;
    },
  };
}
export async function login(page: Page, role: Role = "Usuario") {
  await page.goto("/");
  await page
    .getByLabel("Portal", { exact: true })
    .selectOption(role === "Usuario" ? "user" : "staff");
  await page
    .getByLabel(role === "Usuario" ? "RUT" : "Nombre de usuario", {
      exact: true,
    })
    .fill(
      role === "Usuario"
        ? "12345678-5"
        : role === "Admin"
          ? "admin"
          : "recepcion",
    );
  await page.getByLabel("Contraseña", { exact: true }).fill("password123");
  await page.getByRole("button", { name: "Ingresar", exact: true }).click();
}
export async function navigate(page: Page, label: string) {
  const menu = page.getByRole("button", { name: "Abrir menú" });
  if (await menu.isVisible()) await menu.click();
  await page
    .getByRole("navigation")
    .getByRole("link", { name: label, exact: true })
    .click();
}
export async function reserve(page: Page) {
  await navigate(page, "Reservar");
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page
    .getByRole("button", { name: "Confirmar reserva", exact: true })
    .click();
  await expect(page.locator(".reservation-row")).toHaveCount(1);
}

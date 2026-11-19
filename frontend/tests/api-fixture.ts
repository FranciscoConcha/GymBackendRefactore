import { expect, type Page } from "@playwright/test";
import type { ReservationResponse, UserResponse } from "../src/api";

export async function mockApi(page: Page) {
  // Reloj fijo a las 06:00 de hoy: los bloques del día aún no comienzan sea cual sea la hora de ejecución.
  const morning = new Date();
  morning.setHours(6, 0, 0, 0);
  await page.clock.setFixedTime(morning);
  const student: UserResponse = {
    id: 1,
    rut: "12345678-9",
    name: "Alex Soto",
    email: "alex@example.invalid",
    username: "alex",
    userType: "Estudiante",
    role: "Cliente",
    enabled: true,
  };
  const admin: UserResponse = {
    ...student,
    id: 2,
    name: "Recepción",
    username: "admin",
    role: "Admin",
    userType: "Administrador",
  };
  const users = [student, admin];
  const reservations: ReservationResponse[] = [];
  let session: UserResponse | null = null;
  let inside = false;
  let conflict = false;
  const mutations: string[] = [];
  const slots = (date: string) => [
    {
      id: 1,
      day: "Lunes",
      startTime: "08:00",
      endTime: "09:30",
      zone: "Sala",
      capacity: 7,
      reservedCount: reservations.filter(
        (r) => r.date === date && r.status !== "Cancelado",
      ).length,
      date,
    },
    {
      id: 2,
      day: "Lunes",
      startTime: "10:00",
      endTime: "11:30",
      zone: "Sala",
      capacity: 3,
      reservedCount: 3,
      date,
    },
  ];
  await page.route("**/api/v1/**", async (route) => {
    const request = route.request();
    const method = request.method();
    const url = new URL(request.url());
    const path = url.pathname.replace("/api/v1", "");
    const send = (body: unknown, status = 200) =>
      route.fulfill({ status, json: body });
    if (["POST", "PUT", "PATCH", "DELETE"].includes(method)) {
      expect(request.headers()["x-gym-csrf"]).toBe("1");
      mutations.push(`${method} ${path}`);
    }
    const body = request.postDataJSON();
    if (path === "/auth/login" || path === "/auth/admin-login") {
      if (body.password !== "correcta")
        return send({ message: "Credenciales incorrectas" }, 401);
      session = path === "/auth/admin-login" ? admin : student;
      return send({
        message: "Sesión iniciada",
        user: session,
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      });
    }
    if (!session) return send({ message: "Sin sesión" }, 401);
    if (path === "/auth/me") return send(session);
    if (path === "/auth/logout") {
      session = null;
      return send({ message: "Sesión cerrada" });
    }
    const adminOnly =
      path === "/auth/users" ||
      path.startsWith("/auth/users/") ||
      path === "/auth/register" ||
      path.startsWith("/attendance/") ||
      (path === "/reservations" && method === "GET");
    if (adminOnly && session.role !== "Admin")
      return send({ message: "Sin permiso" }, 403);
    if (path === "/schedules")
      return send(slots(url.searchParams.get("date")!));
    if (path === "/reservations" && method === "POST") {
      const slot = slots(body.date).find((s) => s.id === body.scheduleSlotId);
      if (conflict || !slot || slot.reservedCount >= slot.capacity)
        return send({ message: "No quedan cupos" }, 409);
      const reservation: ReservationResponse = {
        id: reservations.length + 1,
        userId: session.id,
        userName: session.name,
        scheduleSlotId: slot.id,
        date: slot.date,
        day: slot.day,
        startTime: slot.startTime,
        endTime: slot.endTime,
        zone: slot.zone,
        capacity: slot.capacity,
        status: "Reservado",
        createdAt: new Date().toISOString(),
      };
      reservations.push(reservation);
      return send({ message: "Reserva creada", reservation });
    }
    if (path === "/reservations" || path.startsWith("/reservations/user/"))
      return send(
        session.role === "Admin"
          ? reservations
          : reservations.filter((r) => r.userId === session!.id),
      );
    if (path.startsWith("/reservations/") && method === "DELETE") {
      const reservation = reservations.find(
        (r) => r.id === Number(path.split("/").at(-1)),
      );
      if (!reservation) return send({ message: "Reserva no encontrada" }, 404);
      reservation.status = "Cancelado";
      return send({ message: "Reserva cancelada", reservation });
    }
    if (path === "/qr/my-code") {
      const date = new Date();
      const day = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
      return send({
        rut: student.rut,
        name: student.name,
        date: day,
        token: "qr-personal-del-dia",
        validFrom: `${day}T00:00:00`,
        validUntil: `${day}T23:59:59`,
      });
    }
    if (path.startsWith("/attendance/")) {
      const entry = path.endsWith("/entry");
      if (body.token !== "qr-personal-del-dia" || entry === inside)
        return send({ message: "Acceso no permitido" }, 400);
      inside = entry;
      reservations
        .filter((r) => r.status === "Reservado")
        .forEach((r) => {
          r.status = "Asistió";
        });
      return send({
        message: "Registro confirmado",
        attendance: { id: 1, userId: student.id },
      });
    }
    if (path === "/auth/users") return send(users);
    if (path.endsWith("/status")) {
      const user = users.find((u) => u.id === Number(path.split("/")[3]))!;
      user.enabled = body.enabled;
      return send(user);
    }
    if (path === "/auth/register") {
      users.push({
        ...student,
        ...body,
        id: users.length + 1,
        role: body.userType === "Administrador" ? "Admin" : "Cliente",
      });
      return send({ message: "Usuario registrado" });
    }
    throw new Error(`Petición inesperada: ${method} ${path}`);
  });
  return {
    student,
    admin,
    reservations,
    mutations,
    expire: () => {
      session = null;
    },
    restore: (role: "student" | "admin") => {
      session = role === "admin" ? admin : student;
    },
    conflict: () => {
      conflict = true;
    },
  };
}

export async function login(page: Page, admin = false) {
  await page.goto("/");
  await page
    .getByRole("combobox", { name: "Portal", exact: true })
    .selectOption(admin ? "admin" : "user");
  await page
    .getByLabel(admin ? "Usuario" : "RUT", { exact: true })
    .fill(admin ? "admin" : "12345678-9");
  await page.getByLabel("Contraseña", { exact: true }).fill("correcta");
  await page.getByRole("button", { name: "Ingresar", exact: true }).click();
  await expect(page.locator(".topbar")).toBeVisible();
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
  await page.getByRole("button", { name: "Reservar un horario" }).click();
  await page
    .getByRole("button", { name: "Reservar", exact: true })
    .first()
    .click();
  await page
    .getByRole("button", { name: "Confirmar reserva", exact: true })
    .click();
  await expect(page.locator(".reservation-row")).toHaveCount(1);
}

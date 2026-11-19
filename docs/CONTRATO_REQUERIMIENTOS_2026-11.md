# Contrato de implementación: requerimientos del 2026-11-18 (F7–F11)

Fuente: «UCN Gym: requerimientos de backend» (2026-11-18) y las decisiones del usuario del 2026-11-18. Las brechas de partida están en
`REQUERIMIENTOS_2026-11-18_BRECHAS.md`. Base de rutas: `/api/v1`. Todas las respuestas usan JSON camelCase; los errores siguen `{ "message": string }`.
Instantes en UTC (ISO 8601); fechas de sesión locales `YYYY-MM-DD` y horas `HH:mm` en `America/Santiago` (`IGymClock`).

## Decisiones del usuario
| Tema | Decisión |
|---|---|
| Ramas | Una por persona; la reserva guarda una copia de la categoría y la rama. |
| Prioridad | **Ventanas por categoría** (anticipación configurable por categoría). Deportistas: reservas ilimitadas en cualquier bloque. Estudiantes: 1 bloque por día. Sin más límites por categoría. Por ahora se prioriza a deportistas; los valores quedan configurables. |
| Contraseñas | Las cuentas creadas a mano pueden llevar una contraseña temporal (cambio obligatorio). Las cuentas importadas o creadas sin contraseña reciben un **correo de activación** para fijar la suya. Hay recuperación de contraseña por correo. |
| Grupos | Solo los administradores reservan **por rama**, en dos modos: **por cantidad de cupos** o **por el total de plazas disponibles** del bloque. Son reservas no nominativas: entra cualquier deportista habilitado de la rama, hasta el número de cupos. |
| Excel | Se autoriza ClosedXML. |
| Importación | Solo altas. Los existentes (RUT o correo) se omiten y se informan. Cada alta recibe un correo de activación. |
| Pruebas | Se implementa todo y las pruebas se ejecutan al final. |

## 1. Identidad y cuentas (F7)
- **Roles:** `Admin`, `Recepcionista` y `Usuario`. **Categorías:** `Deportista`, `Estudiante`, `Funcionario` y `Externo`. Las categorías no otorgan permisos.
- **`User.AccountStatus`:** `Pendiente` (sin contraseña utilizable, a la espera de activación) o `Activa`. `Enabled = false` equivale a «Deshabilitada».
- **`UserResponse`:** `id, rut, firstName, lastName, fullName, email, username, role, category, sportBranch {id,name}|null, enabled, status ("Pendiente"|"Activa"|"Deshabilitada"), mustChangePassword, createdAt, updatedAt`.
- **`POST /auth/register` (Admin):** `password` pasa a ser **opcional**.
  - Con contraseña: cuenta `Activa` con `mustChangePassword = true`.
  - Sin contraseña: cuenta `Pendiente` y se envía una invitación de activación.
- **`POST /auth/activate`** `{ token, newPassword }` (público, rate limit) → 200 `{ message }`. Deja la cuenta `Activa`, fija la contraseña y anula los demás tokens de activación. Un token inválido, vencido o usado → 400 «El enlace no es válido o expiró.».
- **`POST /auth/password-resets`** `{ email }` (público) → **siempre** 202 `{ message: "Si el correo está registrado, recibirás un enlace para restablecer tu contraseña." }`.
  - Solo envía el correo a cuentas `Activa` y habilitadas.
  - Límite por IP (rate limiter) y por cuenta (máximo 3 por hora; las solicitudes que excedan el límite no envían nada).
- **`POST /auth/password-resets/confirm`** `{ token, newPassword }` → 200. Fija la contraseña, `mustChangePassword = false` y `SessionVersion++`.
- **`POST /auth/users/{id}/invitation` (Admin):** reenvía la activación. Solo aplica a cuentas `Pendiente`; en otro caso → 409.
- **Contraseña nueva:** de 8 a 128 caracteres. Mientras falte la política institucional no se aplican más reglas.
- **Tokens de cuenta:** entidad `AccountToken { Id, UserId, Purpose (Activation|PasswordReset), TokenHash (SHA-256 de 32 bytes aleatorios en Base64Url), CreatedAt, ExpiresAt, UsedAt }`.
  - Vigencia configurable: `Accounts:ActivationHours` = 72 y `Accounts:ResetMinutes` = 60. Un solo uso.
  - **Nunca** se guarda ni se registra el token en claro.
- **Enlaces:** `{Accounts:FrontendBaseUrl}/activar?token=…` y `/restablecer?token=…`.
  - `FrontendBaseUrl` es una opción validada al arrancar (URL absoluta https, o http solo en Development). **Nunca** se deriva de `Host`.
- **Correo:** `EmailOutbox { Id, To, Subject, Body (texto), CreatedAt, AttemptCount, NextAttemptAt, SentAt, LastError }` se escribe en la misma transacción que la operación.
  - Un `BackgroundService` lo despacha con reintentos exponenciales (máximo 5).
  - Envío por `Email:Mode`:
    - `Smtp`: `System.Net.Mail` con `Host`, `Port`, `EnableSsl`, `User`, `Password` y `From`;
    - `Pickup`: escribe `.eml` en `Email:PickupDirectory` (solo desarrollo).
  - `LastError` no incluye el cuerpo del mensaje.
- **Sesiones:** `SessionVersion` en el JWT (`sv`). El logout, el cambio y el reset de contraseña la incrementan.

## 2. Reservas individuales y ventanas por categoría (F9)
- **`Reservations:CategoryMaxDaysAhead`:** `{ "Deportista": n, "Estudiante": n, "Funcionario": n, "Externo": n }`. Si falta una categoría, se usa `Reservations:MaxDaysAhead`.
  - Se aplica con la misma unidad `HorizonUnit`. El personal (sin categoría) usa `MaxDaysAhead`.
- **Límite diario:** `Estudiante` → máximo 1 reserva `Reservado`/`Asistió` por fecha (409). Las demás categorías no tienen límite.
- **Estados de reserva:** `Reservado`, `Cancelado`, `Asistió` e `Inasistencia` (nuevo).
- **Cupo del bloque** = reservas individuales activas (`Reservado`/`Asistió`) + cupos de grupos activos. `ScheduleResponse.reservedCount` ya lo incluye.
  - Añade `groupSeats` y `individualReservations` al desglose.
- **`Idempotency-Key`** (opcional) en `POST /reservations`.

## 3. Reservas grupales por rama (F9)
- **`GroupBooking`:** `{ Id, SportBranchId, ScheduleSlotId, Date, Mode (Cupos|TotalDisponible), Seats, Status (Activa|Cancelada), CreatedById, CreatedAt, CancelledAt, IdempotencyKey }`.
- **`POST /group-bookings` (Admin):** `{ sportBranchId, scheduleSlotId, date, mode: "Cupos"|"TotalDisponible", seats? }`. La cabecera **`Idempotency-Key` es obligatoria** (400 si falta).
  - El modo `Cupos` exige `seats ≥ 1`.
  - El modo `TotalDisponible` toma todos los cupos libres en ese momento (409 si no queda ninguno).
  - **Anticipación de 60 min o más** con el reloj del servidor (60 exactos se aceptan; 59, no) → 400.
  - La rama debe estar habilitada.
  - **Concurrencia:** bloqueo del bloque `FOR UPDATE` en la misma transacción que las reservas individuales. Todo o nada; si no caben los cupos → 409.
  - Responde 200 `{ message, groupBooking }`.
- **`GroupBookingResponse`:** `{ id, sportBranch {id,name}, scheduleSlotId, date, startTime, endTime, mode, seats, usedSeats, status, createdBy (fullName), createdAt }`.
- **Otros endpoints:**
  - `GET /group-bookings?from=&to=&branchId=` (Admin).
  - `POST /group-bookings/{id}/cancel` (Admin; solo antes del inicio).
  - `GET /me/group-bookings` (un deportista ve los grupos activos de su rama desde hoy).
- **Uso en el acceso:** un deportista habilitado de la rama, sin reserva individual en la ventana, entra con el grupo activo de su rama cuya ventana esté abierta.
  - `usedSeats` = número de **personas distintas** que entraron con ese grupo. Si `usedSeats ≥ Seats`, se rechaza a una persona nueva.
  - Quien ya usó un cupo del grupo puede volver a entrar.

## 4. Acceso, visitas y aforo (F8)
- **`Gym:MaxOccupancy`** = 30 (validado, > 0). El aforo actual cuenta las **personas con una visita abierta**.
- **`Visit`:** `{ Id, UserId, ReservationId?, GroupBookingId?, Date, EntryAt, ExitAt?, EntryOperatorId, ExitOperatorId?, DocumentType }`.
  - Índice único parcial: como máximo una visita abierta por usuario (`ExitAt IS NULL`).
  - `DocumentType`: `CarnetIdentidad`, `TNE` o `CredencialUniversitaria`.
- **`AccessEvent`:** `{ Id, OccurredAt, Action (Validar|Entrada|Salida), Result (Aceptado|Rechazado), Reason?, UserId?, OperatorId }`. Registra los resultados, también los rechazos.
- **Sustituye** a la tabla `Attendance` y a `/attendance/*`, que se eliminan. No hay datos que conservar.
- **Ventana de ingreso:** `[inicio − 15 min, fin)`. La salida siempre se permite si hay una visita abierta, aunque la cuenta esté deshabilitada o la sesión haya terminado.
- **Endpoints (Admin y Recepcionista; rate limit `access` por operador):**
  - **`POST /access/validate`** `{ token }` → 200 `{ user: { id, firstName, lastName, fullName, rut, category, sportBranch }, session: { kind: "Reserva"|"Grupo", reservationId?, groupBookingId?, date, startTime, endTime } | null, inside: bool, canEnter: bool, reason?: string, occupancy: { current, max } }`.
    - **No registra la entrada.**
  - **`POST /access/entry`** `{ token, documentType }` → vuelve a validar todo. Admite `Idempotency-Key`.
    - Comprueba: cuenta habilitada, que la persona no esté dentro (409), elegibilidad (reserva o grupo en la ventana) y aforo.
    - Exclusión mutua global con `pg_advisory_xact_lock` para el último lugar: si el aforo está lleno → 409 «Se alcanzó el aforo máximo de la sala.».
    - Crea la `Visit`. En la primera entrada, la reserva pasa a `Asistió`.
    - Responde 200 `{ message, visit }`.
  - **`POST /access/exit`** `{ token }` → cierra la visita abierta (sin visita abierta → 400). Responde 200 `{ message, visit }`.
  - **`GET /access/current`** → `{ occupancy: { current, max }, people: [{ userId, fullName, rut, category, entryAt, sessionEndTime }] }`.
- **`VisitResponse`:** `{ id, userId, fullName, rut, reservationId, groupBookingId, date, entryAt, exitAt, stayMinutes, documentType, entryOperator, exitOperator }`.
- **Historial:**
  - `GET /visits?from=&to=&userId=&branchId=&category=` (solo Admin).
  - `GET /me/visits` (las propias).
- **QR:** `GET /qr/my-code` se mantiene (token personal firmado del día). Añade `firstName, lastName, category, sportBranch` y `nextSession` (la próxima reserva de hoy o del grupo de su rama, o `null`).

## 5. Inasistencias y alertas (F10)
- **Job** (`BackgroundService`, cada `Alerts:NoShowIntervalMinutes` = 15): las reservas `Reservado` cuyo bloque terminó (fecha + fin, en hora de Santiago) y que no tienen ninguna visita pasan a `Inasistencia` y crean una `AttendanceAlert { Id, UserId, ReservationId (único), CreatedAt, AcknowledgedAt? }`.
  - Es idempotente. Las cancelaciones no generan alertas; una visita breve cuenta como asistencia. Los grupos no generan inasistencias individuales.
- **Endpoints:**
  - `GET /me/alerts` → `[{ id, reservationId, date, startTime, endTime, createdAt, acknowledgedAt }]`, primero las no leídas.
  - `POST /me/alerts/{id}/acknowledge` → 200; solo el titular, en otro caso 404. Leer una alerta **no borra** la inasistencia.
  - `GET /admin/alerts?from=&to=` (Admin).
- Sin sanciones ni cambios de cuota.

## 6. Métricas (F11)
**`GET /admin/metrics?from=&to=&branchId=&category=`** (Admin; por defecto, los últimos 30 días). Responde:
```
{ period: { from, to }, filters: { branchId, category },
  occupancy: { current, max },                 // siempre global, aunque se filtre
  reservations: { total, attended, cancelled, noShow },
  groupBookings: { count, seats, usedSeats },
  uniqueVisitors, visits, averageStayMinutes,  // permanencia media de visitas cerradas; null si no hay
  demandByHour: [{ startTime, reservations }],
  demandByBranch: [{ branchId, name, reservations }],
  enabledUsersByCategory: [{ category, count }] }
```
Los filtros de rama y categoría usan la **copia** guardada en la reserva. No se divide por cero.

## 7. Excel (F11, ClosedXML)
- **`POST /admin/exports`** `{ from?, to?, branchId?, category? }` (Admin) → archivo `.xlsx` (`Content-Disposition: attachment`).
  - Hojas:
    - `Filtros`: lo solicitado y la zona horaria.
    - `Usuarios`: RUT, nombre, apellido, correo, rol, categoría, rama y estado.
    - `Sesiones`: fecha, inicio, fin, capacidad, recinto, reservas y cupos de grupo.
    - `Reservas`: id, titular, RUT, fecha, inicio, estado, creada, categoría y rama.
    - `Grupos`: rama, fecha, horario, modo, cupos, usados, creador y estado.
    - `Visitas`: usuario, RUT, entrada, salida, permanencia, documento y operadores.
    - `Inasistencias`: reserva, usuario, fecha y acuse.
  - **Neutraliza fórmulas:** las celdas que empiezan por `= + - @`, tabulador o retorno de carro se anteponen con `'`.
  - Límite de 100 000 filas por hoja (400 si se supera). Audita `export.created`.
  - **Nunca** incluye contraseñas, hashes, tokens ni secretos.
- **`POST /admin/user-imports`** (Admin, multipart con el campo `file`; máximo 2 MB, 1000 filas):
  - El archivo debe ser un `.xlsx` real que ClosedXML pueda abrir; si no → 400.
  - Encabezados `RUT, Nombre, Apellido, Correo, Categoria, Rama`, sin distinguir mayúsculas ni tildes.
  - Responde una **vista previa** `{ id, expiresAt, summary: { create, skip, error }, rows: [{ row, rut, firstName, lastName, email, category, branch, action: "Crear"|"Omitir"|"Error", messages: [] }] }`.
  - Reglas por fila:
    - RUT con DV y normalizado; correo normalizado.
    - Categoría del catálogo; rama obligatoria para deportistas y existente y habilitada; sin rama para el resto.
    - Duplicados dentro del archivo → Error. Existentes por RUT o correo → Omitir.
    - **Nunca** crea personal.
  - Se guarda `UserImport { Id, CreatedById, CreatedAt, ExpiresAt (1 h), Status (Pendiente|Confirmada|Expirada), PayloadJson, ResultJson }`. **No crea usuarios ni envía correos.**
- **`POST /admin/user-imports/{id}/confirm`** (Admin) → vuelve a validar.
  - En una transacción, crea las filas `Crear` como cuentas `Pendiente` y encola sus invitaciones (outbox).
  - Responde `{ created, skipped, failed, rows }`. Si se repite, devuelve el mismo resultado sin duplicar.
  - Audita `import.confirmed`.

## 8. Idempotencia
- **`IdempotencyRecord`:** `{ Key, UserId, Method, Path, RequestHash, StatusCode, ResponseBody, CreatedAt }`, con clave única `(UserId, Key)` y retención de 24 h.
- **Se aplica a:** `POST /reservations`, `POST /group-bookings` (obligatoria), `POST /access/entry` y `POST /admin/user-imports/{id}/confirm`.
- La misma clave con el mismo cuerpo repite la respuesta guardada. Con otro cuerpo → 422 «La clave de idempotencia ya se usó con otra solicitud.».

## 9. Autorización (resumen)
| Endpoint | Admin | Recepcionista | Usuario |
|---|---|---|---|
| Acceso (`/access/*`) | Sí | Sí | No |
| Usuarios, importar, exportar, métricas, alertas globales, visitas globales, grupos (crear, listar, cancelar), ramas (escribir) | Sí | No (403) | No (403) |
| Reservar | Sí | No | Sí |
| `/me/*`, QR propio | Sí | Sí | Sí |

## 10. Pendientes que no se inventan
Planes y cuotas semanales; sanciones; los valores concretos de las ventanas por categoría; el proveedor SMTP institucional; la retención de datos.

# Encargo F4-1: esquema nuevo, reservas por fecha concreta y restricciones (H5 aprobado)

## Objetivo
Rediseñar el modelo de datos (sin datos que conservar) y adaptar servicios, DTOs y pruebas para que **las reservas sean por fecha concreta** en hora del gimnasio (America/Santiago, vía `IGymClock`). Añadir restricciones de integridad. Yo generaré la migración `InitialCreate` con `dotnet ef` después de tu trabajo; tú dejas el modelo y su configuración listos y una fábrica de diseño.

## Contexto verificado
- Repo `C:\Dev\gymbackRefactor`, rama `feat/f4-schema-by-date`. **Tu sandbox no compila, no restaura, no ejecuta Docker ni escribe en `.git`**: escribe código que compile a la primera; yo compilo, genero la migración y ejecuto las pruebas.
- .NET 10, EF Core 10.0.13 + Npgsql 10.0.3 (soporta `DateOnly`→`date`, `TimeOnly`→`time`, `DateTime` UTC→`timestamptz`). Lee primero: `src/GymBackend/Models/*`, `Data/AppDbContext.cs`, `Data/DevelopmentSeeder.cs`, `Domain/*`, `Time/IGymClock.cs`, `Options/*` + `Extensions/ServiceCollectionExtensions.cs` (patrón de opciones validadas), `Application/**` (servicios actuales con `ServiceResult<T>`), `Controllers/*`, `DTOs/**`, y las pruebas en `tests/**` (fixture `GymApiFactory` con `WithClock(FakeTimeProvider)`, `WithDbAsync`, `CreateSlotAsync`, helpers `Infrastructure/Api.cs`, `GymTime`).
- Las pruebas de integración inicializan la BD con `Database__Initialize=true` en Development (seeder). Hoy el seeder usa `EnsureCreatedAsync`.

## 1. Modelo y configuración EF
- `ReservationStatus` enum en `Domain/` (`Reserved`, `Cancelled`, `Attended`) persistido como **texto con los valores actuales** `"Reservado"`, `"Cancelado"`, `"Asistió"` (value converter); en JSON se siguen devolviendo esas cadenas (usar `ReservationStatuses` existentes para mapear). Mantén las constantes de `Domain/ReservationStatuses`.
- `User`: sin cambios de propiedades. Índices únicos: `Rut`; `Email` (se guarda ya normalizado en minúsculas); `Username` único **filtrado** `"Username" <> ''` (clientes tienen `""`). Navegaciones `Reservations`, `Attendances` opcionales.
- `ScheduleSlot`: `DayOfWeek Day` (enum .NET, persistido como entero), `TimeOnly StartTime`, `TimeOnly EndTime`, `Zone`, `Capacity`, `Enabled`. Único (`Day`,`StartTime`,`EndTime`,`Zone`). Checks: `Capacity > 0`, `EndTime > StartTime`.
- `Reservation`: `Id`, `UserId` (FK `User`, `Restrict`), `ScheduleSlotId` (FK `ScheduleSlot`, `Restrict`), `DateOnly Date`, `ReservationStatus Status`, `DateTime CreatedAt` (UTC). Retirar `UserName`, `Day`, `StartTime`, `EndTime`, `Zone`, `Capacity` (D7). Índice único **parcial** (`UserId`,`ScheduleSlotId`,`Date`) filtrado `"Status" = 'Reservado'`; índice (`ScheduleSlotId`,`Date`,`Status`) para conteo de cupos.
- `Attendance`: `Id`, `UserId` (FK), `DateOnly Date`, `DateTime? EntryTime`, `string? EntryAdminRut`, `DateTime? ExitTime`, `string? ExitAdminRut`, `int? ReservationId` (FK `Reservation`, `SetNull` o `Restrict`), `bool CheckedIn`. Retirar `Rut` y `UserName` (se obtienen del usuario). Único (`UserId`,`Date`).
- Configuración con `IEntityTypeConfiguration<T>` en `Data/Configurations/` y `modelBuilder.ApplyConfigurationsFromAssembly`. Longitudes razonables (`Rut` 9, `Email` 256, `Username` 64, `Zone` 64, `Status` 16…).
- `Data/DesignTimeDbContextFactory.cs` (`IDesignTimeDbContextFactory<AppDbContext>`) con `UseNpgsql("Host=localhost;Database=gym_design;Username=design;Password=design")` (solo para generar migraciones; no conecta).
- Seeder: `MigrateAsync` en lugar de `EnsureCreatedAsync`; siembra los 24 bloques con `DayOfWeek.Monday..Saturday` y `TimeOnly` (mismos rangos y cupo). El resto igual.

## 2. Opciones de reserva
`Options/ReservationOptions` (sección `Reservations`): `int MaxDaysAhead = 7` (validar 0..60), `ReservationHorizonUnit HorizonUnit = CalendarDays` (enum `CalendarDays`, `BusinessDays`; días hábiles = lunes a viernes, sin feriados). Registrar validado al arrancar y añadir a `appsettings.json` (`"Reservations": { "MaxDaysAhead": 7, "HorizonUnit": "CalendarDays" }`). Regla pura `Application/Reservations/BookingWindow.LastAllowedDate(DateOnly today, int maxDaysAhead, ReservationHorizonUnit unit)` (con BusinessDays avanza N días hábiles desde hoy).

## 3. API y reglas (mensajes exactos)
- `GET /api/schedules?date=yyyy-MM-dd` (opcional; por defecto `clock.Today`). Fecha mal formada → 400 `"Fecha inválida. Use el formato yyyy-MM-dd."` (recibe `string? date` y parsea con `DateOnly.TryParseExact(..., "yyyy-MM-dd", InvariantCulture)`). Devuelve bloques habilitados con `Day == date.DayOfWeek`, ordenados por `StartTime`, con `reservedCount` = reservas `Reserved` de ese bloque **y esa fecha**. Respuesta: `{ id, day, startTime, endTime, zone, capacity, reservedCount, date }` con `day` en español (`GymWeekdays.Name`), horas `"HH:mm"`, `date` `"yyyy-MM-dd"`.
- `POST /api/reservations` con `CreateReservationRequest { int ScheduleSlotId; DateOnly Date }`. Orden de comprobaciones:
  1. id de usuario nulo en claims → 401 (como hoy, en el controlador);
  2. bloque inexistente o deshabilitado → 400 `"El bloque seleccionado no está disponible."`;
  3. `Date.DayOfWeek != slot.Day` → 400 `"La fecha no corresponde al día del bloque."`;
  4. fecha pasada, o fecha == hoy y `clock.LocalNow` (hora local) ≥ `StartTime` → 400 `"El bloque ya comenzó o la fecha ya pasó."`;
  5. `Date > BookingWindow.LastAllowedDate(...)` → 400 `"La fecha excede el plazo máximo de reserva."`;
  6. usuario inexistente → 401 `"La sesión no corresponde a un usuario válido."` (mensaje actual);
  7. cupo: reservas `Reserved` de (bloque, fecha) ≥ `Capacity` → 400 `"No quedan cupos disponibles para este bloque."`;
  8. duplicada activa (usuario, bloque, fecha) → 400 `"Ya tienes una reserva para este bloque."`;
  éxito 200 `{ message: "Reserva creada correctamente.", reservation }`. (Usuario deshabilitado sigue pudiendo reservar: LegacyBehavior D2 hasta F5.)
- Respuesta de reserva: `{ id, userId, userName, scheduleSlotId, date, day, startTime, endTime, zone, capacity, status, createdAt }` (derivados del bloque y del usuario; `Include` necesario).
- `GET /api/reservations` (admin) y `GET user/{id}`: mismas reglas de autorización; orden `Date` desc, luego `StartTime` desc.
- `DELETE /api/reservations/{id}`: 404 si no existe; autorización como hoy; si `Status != Reserved` → 400 `"Solo se pueden cancelar reservas activas."` (corrige D13); si NO es admin y (fecha pasada o (fecha == hoy y hora local ≥ inicio)) → 400 `"La reserva ya no se puede cancelar porque el bloque comenzó."`; éxito 200 `{ message: "Reserva cancelada correctamente.", reservation }`.
- Asistencia (`AttendanceService`): la reserva candidata es la del usuario con `Date == clock.Today`, `Status == Reserved` y dentro de la ventana ±15 min (`ScheduleWindow`, adáptala a `TimeOnly`). Misma lógica de reingreso y mismos mensajes. `Attendance.Date = clock.Today`. Respuesta `attendance` igual que hoy (`rut`, `userName` desde el usuario; `date` `"yyyy-MM-dd"`).
- `QrService` y Auth: solo lo necesario para compilar.

## 4. Pruebas (actualizar y añadir)
- Helpers: `CreateSlotAsync(DayOfWeek day, TimeOnly start, TimeOnly end, int capacity = 20)` (zona única); `Api.Reserve(client, slotId, date)`; `GymTime.Tomorrow()`. Para pruebas con reloj real, reservar **mañana** (fecha de Santiago) en un bloque del día de la semana de mañana. Si una prueba necesita una reserva que la API ya no permitiría crear (p. ej. hoy en un bloque pasado), insértala con `WithDbAsync`.
- Actualizar todas las pruebas existentes al nuevo contrato (Reservations, Schedules, Attendance, Time, Contracts, Smoke/seed con `DayOfWeek`). Reemplazar la prueba LegacyBehavior D13 por la nueva regla (cancelar no activa → 400).
- Nuevas (con `factory.WithClock(...)` cuando dependan de la hora; instantes **anteriores a 2026-11-18T03:00Z**): bloque de otro día → 400; fecha pasada → 400; hoy antes del inicio → 200 y en el inicio exacto → 400; límite de anticipación exacto → 200 y +1 día → 400; modo `BusinessDays` (configúralo con `WithWebHostBuilder(b => b.UseSetting("Reservations:HorizonUnit", "BusinessDays"))` o `ConfigureAppConfiguration`); cupo por fecha (lleno el martes 17 no afecta al martes 24); `GET /api/schedules?date=` filtra por día, domingo → lista vacía, fecha inválida → 400; cancelación cliente después del inicio → 400 y admin → 200; asistencia usa la reserva de hoy (una reserva de otra fecha no habilita el ingreso).
- Restricciones en BD (`Data/ConstraintTests.cs` en integración): insertar vía `WithDbAsync` duplicados de RUT, de correo, de username no vacío, dos reservas activas iguales, dos asistencias mismo (usuario, fecha), bloque con `Capacity = 0` → `DbUpdateException`; dos clientes con `Username = ""` → permitido; reserva cancelada + nueva activa igual → permitido.
- Unitarias: `BookingWindow` (días corridos; días hábiles cruzando fin de semana, p. ej. viernes + 1 hábil = lunes; `MaxDaysAhead = 0` = hoy), `ScheduleWindow` adaptado.
- Contratos: actualizar formas de `reservation` y `schedule` a lo indicado.

## Archivos escribibles
`src/GymBackend/**` (excepto `GymBackend.csproj`, `Data/Migrations/**`), `tests/**`.

## Prohibiciones
- No crear archivos de migración a mano (los genero yo). No añadir paquetes. No modificar CI, Dockerfile, compose ni docs. Sin comandos git de escritura.
- No cambiar Auth/QR/hash más allá de compilar. No cambiar mensajes existentes que no se mencionan aquí.
- Si necesitas salir del alcance, detente y reporta FUERA_DE_ALCANCE.

## Criterios de aceptación
1. Modelo y configuración según §1; fábrica de diseño lista.
2. Reglas de §3 con los mensajes exactos.
3. Pruebas de §4 escritas.
4. (Verifico yo) migración generada, build sin errores, todas las pruebas en verde.

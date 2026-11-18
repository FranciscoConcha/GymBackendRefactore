# Plan de refactorización — UCN Gym API (Back_Gym)

- **Origen:** `C:\Dev\gymUCN\Back_Gym` (remoto actual `xAchaoooo/Back_Gym`, 1 commit `403f81a`, árbol limpio).
- **Destino:** `https://github.com/FranciscoConcha/GymBackendRefactore.git` (público, vacío).
- **Espacio de trabajo:** `C:\Dev\gymbackRefactor`.
- **Stack:** ASP.NET Core .NET 10 (SDK 10.0.400 instalado), EF Core 10 + Npgsql, JWT, Docker Compose, GitHub Actions.
- **Clasificación:** crítica (seguridad, esquema de datos, migraciones). Cada fase empieza y termina con aprobación explícita.

## 1. Diagnóstico (verificado leyendo el código)

| # | Hallazgo | Ubicación | Severidad |
|---|---|---|---|
| D1 | Contraseñas con SHA-256 sin sal ni iteraciones; el login busca por hash en la BD | `AuthController.HashPassword`, `Program.cs` bootstrap | Crítica |
| D2 | Un usuario deshabilitado puede iniciar sesión; su JWT sigue válido 8 h | `AuthController.Login/AdminLogin` | Alta |
| D3 | La misma `Jwt:Key` firma JWT y QR (reutilización de clave); JWT sin issuer/audience | `Program.cs`, `QrController`, `AttendanceController` | Alta |
| D4 | Sin limitación de intentos en `login` y `admin-login` | `Program.cs` | Alta |
| D5 | Control de cupo con condición de carrera (cuenta y luego inserta) | `ReservationsController.CreateReservation` | Alta |
| D6 | Sin migraciones (`EnsureCreated`), sin índices únicos (RUT, correo, usuario) ni FK | `Program.cs`, `AppDbContext` | Alta |
| D7 | La reserva referencia el bloque por cadenas (día/hora/zona) y duplica `UserName` y `Capacity` | `Models/Reservation.cs` | Media |
| D8 | Lógica de negocio en controladores; `CurrentUserId` duplicado y lanza excepción que termina en 500 | `Controllers/*` | Media |
| D9 | `DateTime.Now` depende de la zona horaria del servidor; no es testeable | `Attendance`, `Qr` | Media |
| D10 | Cadenas mágicas (`"Admin"`, `"Reservado"`, `"Asistió"`, días en español) | Todo el proyecto | Media |
| D11 | Respuestas con objetos anónimos y entidades EF expuestas; sin validación declarativa ni ProblemDetails | `Controllers/*` | Media |
| D12 | Consulta síncrona `FirstOrDefault` dentro de flujo asíncrono | `AttendanceController.TryResolveUserFromToken` | Baja |
| D13 | Se puede cancelar una reserva ya `Asistió`/`Cancelado` | `ReservationsController.CancelReservation` | Baja |
| D14 | Versiones EF 10.0.0 frente a ASP.NET 10.0.8; Swashbuckle y OpenApi a la vez | `GymBackend.csproj` | Baja |
| D15 | Cero pruebas automatizadas; CI solo en `main` y sin pruebas | `.github/workflows/build.yml` | Alta (bloquea refactorizar con seguridad) |
| D16 | La hora del gimnasio depende de la zona del servidor (`DateTime.Now`); solo Docker fija `TZ=America/Santiago`. En un servidor UTC, la fecha del QR, la ventana de ingreso y el día de la semana se desplazan 3-4 h | `QrController`, `AttendanceController`, `Dockerfile` | Alta |

## 2. Estrategia de ramas (GitHub)

```
main  ←── test  ←── dev  ←── feature/*, refactor/*, fix/*, tests/*, chore/*, docs/*
 (estable,  (QA /       (integración
  tags)      candidata)   continua)
```

- **`main`**: solo recibe PR desde `test`. Cada fusión crea un tag `vX.Y.Z`.
- **`test`**: solo recibe PR desde `dev` (candidata de versión). Aquí se ejecuta la validación completa (pruebas de integración + `docker compose up` + humo manual).
- **`dev`**: integración. Recibe PR de las ramas de tarea.
- **Ramas de tarea**: se crean desde `dev`, con el formato `<tipo>/<fase>-<descripcion-corta>`; por ejemplo, `refactor/f2-options-pattern`. Una tarea = una rama = un PR.
- **Correcciones urgentes**: `fix/*` desde `test` cuando el fallo aparece en QA; después se fusiona también hacia `dev`.
- **Protección** (`gh api`, requiere autorización): las tres ramas sin push directo; PR obligatorio; CI en verde; *squash merge* hacia `dev`; *merge commit* en `dev→test` y `test→main` para conservar la trazabilidad.
- **Commits**: Conventional Commits (`feat:`, `fix:`, `refactor:`, `test:`, `chore:`, `docs:`).
- **CI**: se ejecuta en cada `pull_request` y en cada `push` a `dev`, `test` y `main`: restore, build con advertencias como errores, pruebas y `docker build`.

## 3. Hitos de aprobación

Ningún commit, push, merge, creación de ramas remotas ni cambio de protecciones se ejecuta sin tu "aprobado" explícito en el hito correspondiente.

| Hito | Qué apruebas | Resultado esperado |
|---|---|---|
| **H0** | Este plan y las decisiones de §5 | Plan congelado |
| **H1** | Fase 0 completa: repositorio, ramas, CI y protecciones | `main`, `test` y `dev` publicadas y protegidas |
| **H2** | Red de pruebas de caracterización (F1) en `dev` | Comportamiento actual cubierto |
| **H3** | Promoción `dev→test→main` como `v0.2.0` (estructura sin cambios funcionales: F1+F2) | Primera versión refactorizada |
| **H4** | Capa de servicios (F3) en `dev` | Controladores delgados |
| **H5** | Diseño del esquema y migración inicial (F4), **antes** de implementarlo | Modelo de datos acordado |
| **H6** | Endurecimiento de seguridad (F5), **antes** de fusionar | Cambios de contrato aceptados |
| **H7** | Promoción a `test`: validación en Docker y prueba de humo | Candidata `v1.0.0-rc` |
| **H8** | Promoción a `main` con tag `v1.0.0` | Entrega |

Dentro de cada fase, cada PR de tarea hacia `dev` se presenta con su diff revisado y las pruebas ejecutadas; la fusión requiere tu aprobación, salvo que en H0 autorices fusionar a `dev` sin consulta si la CI está en verde.

## 4. Fases y tareas

Modo de trabajo por tarea: encargo en `.md` → Codex implementa en worktree/rama → yo verifico (`git diff`, alcance, `dotnet build`, `dotnet test`) → PR a `dev`. Hay como máximo tres tareas en paralelo y solo si tocan archivos disjuntos.

### F0 — Preparación del repositorio → **H1**
| Rama | Tarea | Criterio de aceptación |
|---|---|---|
| (directo, autorizado) | Clonar el origen en `C:\Dev\gymbackRefactor` conservando el historial; `origin`→nuevo repositorio; `upstream`→`xAchaoooo/Back_Gym` (solo lectura) | `git log` contiene `403f81a`; el remoto apunta al repositorio nuevo |
| (directo) | Publicar `main`; crear `test` y `dev` desde `main` | Las tres ramas existen en GitHub |
| `chore/f0-ci-branches` | CI para PR y push en `dev`, `test` y `main`; plantilla de PR; `CONTRIBUTING.md` con flujo de ramas; `.editorconfig` | CI en verde en el PR |
| (directo, autorizado) | Protección de ramas (`gh api`) | Push directo rechazado |

### F1 — Red de seguridad: pruebas de caracterización → **H2**
| Rama | Tarea | Criterio |
|---|---|---|
| `chore/f1-solution-layout` | Mover a `src/GymBackend/`, crear `GymBackend.slnx` y `tests/`; adaptar Dockerfile, CI y README | `dotnet build` y `docker build` en verde; mismos endpoints |
| `tests/f1-integration-harness` | `tests/GymBackend.IntegrationTests`: xUnit, `WebApplicationFactory`, Testcontainers PostgreSQL, *helpers* de JWT y *seed* | Arranca la API contra un PostgreSQL efímero |
| `tests/f1-characterization` | Pruebas del comportamiento **actual** de Auth, Schedules, Reservations, Qr, Attendance y health, incluidos los casos de 401/403/400 | ≥1 prueba por endpoint y regla de negocio; todas en verde |

Riesgo: las pruebas de integración requieren Docker en local y en CI (`ubuntu-latest` ya lo incluye).

### F2 — Higiene estructural sin cambio funcional → **H3** (`v0.2.0`)
| Rama | Tarea | Criterio |
|---|---|---|
| `refactor/f2-options-config` | `JwtOptions`, `CorsOptions` y `BootstrapOptions` con `ValidateOnStart`; el *seed* sale de `Program.cs` a `DevelopmentSeeder` | `Program.cs` < 60 líneas; las pruebas F1 siguen en verde |
| `refactor/f2-constants-claims` | Constantes de roles, estados y días; extensión `ClaimsPrincipal.GetUserId()` que devuelve 401 en lugar de 500 | Sin cadenas mágicas en los controladores |
| `refactor/f2-dtos-errors` | DTOs `record` de respuesta (mismo JSON), `ProblemDetails` y manejador global de excepciones | Contrato JSON idéntico (verificado por F1) |
| `refactor/f2-gym-clock` | Zona horaria explícita `Gym:TimeZone` (IANA, por defecto `America/Santiago`, validada al arrancar). Servicio `IGymClock` sobre `TimeProvider` que entrega la fecha, la hora y el día en la zona del gimnasio; se elimina todo `DateTime.Now` (D16). Regla: la BD guarda instantes en UTC y las fechas y horarios de los bloques son hora local del gimnasio. Consulta asíncrona en Attendance (D12) | Pruebas con `FakeTimeProvider` y servidor en UTC: 23:50 de Santiago (el QR mantiene la fecha local), cambio de horario de abril y septiembre, borde de la ventana de ±15 min en UTC−3 y UTC−4 |
| `chore/f2-packages` | Alinear EF/ASP.NET a 10.0.13 y Npgsql a 10.0.3; corregir NU1903 (`Pkcs` vía `EF.Tools`); retirar `Microsoft.AspNetCore.OpenApi` (no se usa; se mantiene Swashbuckle) | `dotnet list package --vulnerable` vacío |

### F3 — Capa de aplicación (servicios) → **H4**
| Rama | Tarea | Criterio |
|---|---|---|
| `refactor/f3-auth-service` | `IAuthService`, `IUserService`, `ITokenService` | Controlador sin `DbContext` |
| `refactor/f3-reservation-schedule-service` | `IReservationService` e `IScheduleService` | Ídem |
| `refactor/f3-attendance-qr-service` | `IAttendanceService`; `QrTokenService` deja de ser estático y pasa a inyectarse | Ídem |
| `tests/f3-unit-tests` | `tests/GymBackend.UnitTests` para reglas puras (ventana horaria, cupo, QR) | Cobertura de reglas de negocio |

Las tres primeras tareas pueden ejecutarse en paralelo porque usan archivos disjuntos; el registro de DI se integra al final.

### F4 — Modelo de datos y migraciones (crítica) → **H5 aprobado (2026-11-17)**

Decisiones de H5: no hay datos que conservar, así que se crea una migración inicial nueva sin *baseline* ni transformación. Las **reservas pasan a ser por fecha concreta** (hora de Santiago) en lugar de semanales.

| Rama | Tarea | Criterio |
|---|---|---|
| `feat/f4-schema-by-date` | Esquema nuevo con migración `InitialCreate` (se reemplaza `EnsureCreated` por `Migrate`, solo en Development). `ScheduleSlot` con `DayOfWeek`/`TimeOnly`; `Reservation` con FK a `User` y a `ScheduleSlot` y `Date` (`DateOnly`); se retiran las columnas duplicadas (D7). `Attendance` con `Date` (`DateOnly`) y FK. Estado de reserva como `enum` persistido como texto. Restricciones: RUT, correo y usuario no vacío únicos; una reserva activa por (usuario, bloque, fecha); una asistencia por (usuario, fecha); checks de cupo y horario. API por fecha y reglas de negocio (ver abajo) | Pruebas de integración y de contrato actualizadas en verde; las restricciones se verifican en la BD |
| `fix/f4-capacity-concurrency` | Cupo atómico mediante bloqueo de fila del bloque en una transacción (D5); violación de unicidad concurrente → 409 | Prueba concurrente: N+1 solicitudes simultáneas → exactamente N reservas |

Reglas de negocio de reserva (H5):
- `GET /api/schedules?date=yyyy-MM-dd` (por defecto, hoy): bloques habilitados del día de la semana de esa fecha, con `reservedCount` de esa fecha.
- `POST /api/reservations { scheduleSlotId, date }`: la fecha debe coincidir con el día del bloque; no se admiten fechas pasadas; el mismo día se puede reservar **hasta la hora de inicio** del bloque.
- **Anticipación máxima configurable** (`Reservations:MaxDaysAhead`, por defecto 7; `Reservations:HorizonUnit` = `CalendarDays` | `BusinessDays`, donde los días hábiles son de lunes a viernes, sin feriados).
- Cancelación: solo reservas en estado `Reservado` (corrige D13). El usuario puede cancelar **hasta el inicio** del bloque; el administrador, siempre.
- La asistencia busca la reserva de **la fecha de hoy** (Santiago) dentro de la ventana de ±15 min.

### F5 — Endurecimiento de seguridad (crítica) → **H6**
| Rama | Tarea | Criterio |
|---|---|---|
| `fix/f5-password-hashing` | `PasswordHasher<User>` (PBKDF2) con migración transparente: si el hash es SHA-256 heredado y coincide, se re-hashea al iniciar sesión (D1) | Pruebas de hash heredado y nuevo |
| `fix/f5-login-enabled` | Rechazar usuarios deshabilitados; invalidar tokens mediante `SecurityStamp` o validación en `OnTokenValidated` (D2) | Usuario deshabilitado → 401 incluso con un token previo |
| `fix/f5-keys-jwt` | Clave QR propia (`Qr:SigningKey`); validar issuer/audience (D3) | Un token QR no sirve como JWT ni al revés |
| `feat/f5-rate-limiting` | `AddRateLimiter` en los endpoints de login (D4) | 429 tras superar el umbral |
| `feat/f5-api-v1` | Rutas versionadas `/api/v1/...`; pruebas actualizadas | Las rutas antiguas responden 404 o redirección documentada |
| `feat/f5-validation` | Validación de DTOs (DataAnnotations); RUT con dígito verificador (según la decisión de §5); reglas de cancelación (D13) | 400 con ProblemDetails |

Decisiones H6 (2026-11-18): RUT canónico `12345678-5` (cuerpo 7-8 dígitos, guion, DV `0-9`/`K`; la entrada acepta puntos y guion); sin conversión de RUT heredados (H5: sin datos que conservar); rutas antiguas `/api/...` → 404. Estado: hashing, login deshabilitado, claves/JWT, rate limiting y RUT en PR #21; `/api/v1` en `feat/f5-api-v1`. Pendiente para F6: `ForwardedHeaders` para que el límite por IP funcione detrás de un proxy.

### F6 — Calidad, documentación y entrega → **H7**, **H8**
| Rama | Tarea | Criterio |
|---|---|---|
| `chore/f6-observability` | Logging estructurado con correlación; *health check* de EF | `/health/ready` usa `AddHealthChecks` |
| `chore/f6-ci-quality` | `TreatWarningsAsErrors`, `dotnet format --verify-no-changes`, informe de cobertura y Dependabot | CI en verde |
| `docs/f6-readme-adr` | README actualizado y ADR breves (capas, hashing, migraciones, ramas) | Documentación coherente con el código |
| Promoción | `dev→test` (H7: `docker compose up` y humo de todos los endpoints) → `test→main` + tag `v1.0.0` (H8) | Versión publicada |

## 5. Decisiones aprobadas en H0 (2026-11-17)

1. **Historial:** se conserva el historial del origen (`403f81a`). `upstream` = `xAchaoooo/Back_Gym` (push deshabilitado).
2. **Pruebas de integración:** Testcontainers PostgreSQL.
3. **Contrato de API:** se versiona como `/api/v1` (tarea `feat/f5-api-v1`, después de la caracterización).
4. **RUT:** se valida el dígito verificador (módulo 11) en `feat/f5-validation`. Se debe definir el formato canónico de almacenamiento y cómo convertir los RUT existentes de 8 a 9 dígitos.
5. **Fusión a `dev`:** autorizada sin consulta cuando la CI está en verde y la revisión independiente no tiene observaciones. `dev→test` y `test→main` requieren hito.
6. **Publicación:** el usuario confirmó que tiene permiso para publicar en el repositorio público.

Adicional: en F0 se detectó `NU1903` (`System.Security.Cryptography.Pkcs` 9.0.0, vulnerabilidad alta, transitiva). Se corrige en `chore/f2-packages`.

Adicional H2 (2026-11-17): la hora del gimnasio se calcula siempre en `America/Santiago` (zona IANA, con horario de verano), independientemente del servidor (D16, `refactor/f2-gym-clock`). Si en el futuro hay sedes en otra zona (por ejemplo, `America/Punta_Arenas`), la zona pasa a configurarse por sede.

## 6. Riesgos

- Repositorio público: no deben subirse `.env`, secretos ni datos reales (`.gitignore` ya los excluye; se revisará antes de cada push).
- F4 cambia el esquema: no hay datos productivos según el README; si existieran, se requiere respaldo y plan de reversión antes de H5.
- F5 cambia el comportamiento de autenticación: los tokens vigentes dejarán de ser válidos (aceptable fuera de producción).
- Límite de cinco ciclos de implementación, verificación y corrección por tarea; si se supera, me detengo e informo.

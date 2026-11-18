# UCN Gym API

API ASP.NET Core .NET 10 con Entity Framework Core y PostgreSQL, extraída de `gymapp-plataformas` en el commit `1ea12e767f1405391f6a4c64178303db956b0a60`. El frontend relacionado está en [UCN_Gym](https://github.com/xAchaoooo/UCN_Gym).

## Estado

La API está refactorizada en capas de aplicación y controladores delgados. Usa migraciones EF Core versionadas, reservas por fecha concreta y controles de seguridad para autenticación, claves, límites de intentos y cabeceras HTTP. La inicialización de desarrollo aplica migraciones mediante `MigrateAsync` cuando `Database:Initialize=true`, únicamente en Development.

## Levantar con Docker Compose

Requisito: Docker Compose. El entorno Compose está configurado para desarrollo y revisión.

1. Copia `.env.example` como `.env`.
2. Define `POSTGRES_PASSWORD`, `JWT_KEY` y `QR_SIGNING_KEY`. Las claves deben tener al menos 32 caracteres; `QR_SIGNING_KEY` debe ser distinta de `JWT_KEY`. Puedes generar valores con `openssl rand -hex 32`.
3. Opcionalmente, completa `JWT_ISSUER` y `JWT_AUDIENCE` para personalizar sus valores. Para crear un administrador inicial, completa `BOOTSTRAP_ADMIN_RUT`, `BOOTSTRAP_ADMIN_EMAIL`, `BOOTSTRAP_ADMIN_USERNAME` y `BOOTSTRAP_ADMIN_PASSWORD`. El RUT debe incluir un dígito verificador válido; la contraseña debe tener al menos 16 caracteres. Sin esos cuatro datos no se crea una cuenta administrativa.
4. Ejecuta `docker compose up --build -d`.

La API queda disponible en `http://localhost:5008`; Swagger está disponible en `/swagger` solo en Development. Si ya existía un volumen de base de datos anterior a F5, recréalo con `docker compose down -v` antes de iniciar el entorno. La base de datos no publica un puerto al host.

## Configuración

Los valores pueden suministrarse mediante variables de entorno usando doble guion bajo en lugar de `:`. En Compose, las claves secretas obligatorias se toman del archivo `.env`.

| Clave | Valor predeterminado / requisito | Uso |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Obligatoria; sin valor predeterminado | Conexión PostgreSQL. |
| `Jwt:Key` | Obligatoria; mínimo 32 caracteres | Firma de JWT. |
| `Jwt:Issuer` | `ucn-gym-api` | Emisor validado del JWT. |
| `Jwt:Audience` | `ucn-gym-frontend` | Audiencia validada del JWT. |
| `Qr:SigningKey` | Obligatoria; mínimo 32 caracteres y distinta de `Jwt:Key` | Firma de tokens QR. |
| `Gym:TimeZone` | `America/Santiago` | Zona horaria IANA del gimnasio. |
| `Reservations:MaxDaysAhead` | `7` (rango 0–60) | Anticipación máxima de reserva. |
| `Reservations:HorizonUnit` | `CalendarDays` (`CalendarDays` o `BusinessDays`) | Unidad del horizonte de reservas. |
| `RateLimiting:Login:PermitLimit` | `5` | Solicitudes permitidas por ventana en login. |
| `RateLimiting:Login:WindowSeconds` | `60` | Duración de la ventana de login. |
| `Cors:AllowedOrigins` | Lista vacía | Orígenes CORS permitidos; Compose agrega `http://localhost:4173` y `http://localhost:8080`. |
| `ForwardedHeaders:Enabled` | `false` | Usa `X-Forwarded-For`/`X-Forwarded-Proto` de proxies de confianza (necesario para el límite de login por IP tras un proxy). |
| `ForwardedHeaders:KnownProxies` / `KnownNetworks` | Listas vacías (más loopback) | IP y redes CIDR de los proxies de confianza; se validan al arrancar. |
| `Database:Initialize` | `false` | Activa la inicialización solo en Development; Compose lo establece en `true`. |
| `Bootstrap:AdminRut` | Sin valor | RUT del administrador inicial; requiere también email, usuario y contraseña. |
| `Bootstrap:AdminEmail` | Sin valor | Correo del administrador inicial. |
| `Bootstrap:AdminUsername` | Sin valor | Usuario del administrador inicial. |
| `Bootstrap:AdminPassword` | Sin valor | Contraseña de al menos 16 caracteres para el administrador inicial. |

## API

Todas las rutas de controladores tienen el prefijo `/api/v1`; las rutas antiguas `/api/...` no están mapeadas y responden 404.

| Controlador | Rutas y métodos | Acceso / función |
|---|---|---|
| `Auth` | `POST /login`, `POST /admin-login`, `POST /register`, `GET /users`, `PATCH /users/{id}/status` | Login de usuario y administrador; registro, listado y habilitación de usuarios para administradores. Login limitado por tasa. |
| `Schedules` | `GET /schedules?date=yyyy-MM-dd` | Consulta bloques habilitados para la fecha (requiere autenticación). |
| `Reservations` | `GET /reservations`, `GET /reservations/user/{userId}`, `POST /reservations`, `DELETE /reservations/{id}` | Consulta, creación y cancelación; el listado general es de administrador. |
| `Qr` | `GET /qr/my-code` | Emite el código personal del usuario autenticado. |
| `Attendance` | `POST /attendance/entry`, `POST /attendance/exit` | Registra entrada y salida; requiere rol administrador. |

Los endpoints de salud son `GET /health/live` y `GET /health/ready`; el segundo comprueba la conexión a la base de datos. Swagger solo se habilita en Development.

## Seguridad

- Las contraseñas nuevas usan PBKDF2 mediante `PasswordHasher<User>`. Al validar correctamente un hash SHA-256 heredado, se reemplaza por un hash nuevo.
- Un usuario deshabilitado recibe 401 al iniciar sesión y sus tokens dejan de ser válidos al validar el usuario.
- JWT usa HS256 con `Jwt:Issuer` y `Jwt:Audience`; los tokens QR usan una clave separada.
- Los endpoints de login comparten un límite de tasa por IP; al excederlo responden 429.
- El RUT se valida y almacena en forma canónica, por ejemplo `12345678-5`.
- La API agrega cabeceras HTTP de seguridad.

El JWT se devuelve en el cuerpo de la respuesta de autenticación; su almacenamiento en el cliente es responsabilidad del frontend.

## Observabilidad

- Cada respuesta incluye `X-Correlation-ID`: se reutiliza el recibido si es válido (1–64 caracteres `[A-Za-z0-9._-]`) o se genera uno. Los errores ProblemDetails lo incluyen como `correlationId` y los logs lo registran en el scope `CorrelationId`.
- Fuera de Development los logs se emiten en JSON por consola, con scopes y marca de tiempo UTC.
- `/health/live` no consulta dependencias; `/health/ready` ejecuta el health check de base de datos y responde 503 (`{"status":"unavailable"}`) si no hay conexión.

## Desarrollo

Desde la raíz del repositorio:

```sh
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet test --collect "Code Coverage" --settings coverage.runsettings
dotnet ef migrations add <Nombre> -p src/GymBackend -o Data/Migrations
```

Las pruebas de integración usan Testcontainers y requieren Docker. El flujo de ramas, los prefijos de rama y las reglas de PR están documentados en [CONTRIBUTING.md](CONTRIBUTING.md).

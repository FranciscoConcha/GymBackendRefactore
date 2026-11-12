# UCN Gym API

Backend ASP.NET Core .NET 10 + Entity Framework Core + PostgreSQL, separado del proyecto Angular original. Frontend: https://github.com/xAchaoooo/UCN_Gym.

## Estado y procedencia

Extraído de `gymapp-plataformas`, commit `1ea12e767f1405391f6a4c64178303db956b0a60`. Se conservan controladores, contratos, modelos y lógica de negocio para su revisión. Se retiraron credenciales incorporadas, cuentas de contraseña conocida, reservas de prueba y cambios de esquema automáticos del arranque habitual. CORS se configura por ambiente; la inicialización y el administrador opcional requieren configuración explícita y se limitan a Development.

Esta base está pendiente de auditoría y endurecimiento; no es una API productiva aprobada. El frontend actual aún utiliza almacenamiento local y no consume esta API.

## Levantar un entorno aislado

Requisitos: Docker Compose. No usar datos ni bases institucionales para la revisión.

1. Copiar `.env.example` como `.env`.
2. Definir una contraseña nueva de PostgreSQL y `JWT_KEY` aleatoria de al menos 32 caracteres. Ejemplo de generación: `openssl rand -hex 32`.
3. Para crear un administrador inicial, completar las cuatro variables `BOOTSTRAP_ADMIN_*` con datos de revisión y una contraseña nueva de al menos 16 caracteres. Sin ellas no se crea ninguna cuenta administrativa.
4. Ejecutar `docker compose up --build -d`.

Swagger: http://localhost:5008/swagger. Salud: `/health/live` y `/health/ready`. Base de datos sin puerto público; persistencia en volumen propio. El Compose es para desarrollo/revisión, no para producción.

La inicialización usa `EnsureCreated` solo en Development y crea horarios base en una base vacía. No actualiza esquemas existentes; faltan migraciones versionadas para la entrega productiva. Retirar las variables bootstrap del ambiente después del primer arranque.

## Compilar sin Docker

```sh
dotnet restore
dotnet build -c Release --no-restore
```

Para ejecutar, suministrar `Jwt__Key`, `ConnectionStrings__DefaultConnection` y `Cors__AllowedOrigins__0` desde el entorno. No escribir secretos en `appsettings.json` ni versionar `.env`.

## API

- `Auth`: login de usuario/administrador, alta administrativa, listado y habilitación de usuarios.
- `Schedules`: bloques semanales y cupos.
- `Reservations`: alta, consulta y cancelación de reservas.
- `Qr`: emisión del código personal.
- `Attendance`: registro de entrada/salida.

Los controles de autenticación y negocio heredados se mantienen para la auditoría; la extracción no implica que sus algoritmos o políticas hayan sido corregidos. Los informes de auditoría no forman parte del repositorio.

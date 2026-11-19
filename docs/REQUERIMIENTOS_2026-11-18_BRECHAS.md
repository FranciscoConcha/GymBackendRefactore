# Brechas frente a «UCN Gym: requerimientos de backend» (2026-11-18)

Contraste del documento de requerimientos con la API refactorizada (`dev`, candidata `v1.0.0`, commit `f86aca2`) y el
frontend integrado en `frontend/`. Cada estado se comprobó en el código o en las pruebas; «No» significa que no existe.

**Conclusión:** la API implementa las **reglas anteriores** que el documento declara reemplazadas (aforo 20 por bloque, roles
`Admin`/`Cliente`, una entrada/salida diaria). Cumple buena parte de los requisitos **transversales** de seguridad e integridad
(§3 hash, §4 concurrencia, §16), pero **no** el modelo de dominio nuevo (recepcionista, categorías, ramas, grupos, visitas con
reingreso, QR en dos pasos, inasistencias, Excel, invitaciones). Esto exige una fase nueva con migraciones y contratos
versionados, y varias decisiones de §18 antes de implementar.

Leyenda: ✅ cumple · 🟡 parcial · ❌ no cumple

## §2 Roles y categorías
| Requisito | Estado | Situación actual |
|---|---|---|
| Roles Administrador / Recepcionista / Usuario | ❌ | Solo `Admin` y `Cliente`; no hay recepcionista. |
| Autorización por endpoint y campos limitados por rol | 🟡 | `[Authorize(Roles)]` en cada endpoint; falta el rol recepcionista y respuestas reducidas. |
| Categorías Deportista/Estudiante/Funcionario/Externo separadas del rol | ❌ | `UserType` = `Estudiante`/`Administrador`, mezclado con el rol. |
| Rama deportiva obligatoria para deportistas | ❌ | No existen ramas. |

## §3 Identidad y cuentas
| Requisito | Estado | Situación actual |
|---|---|---|
| Id interno estable | ✅ | `int` autoincremental. |
| RUT normalizado, único, DV y `K` | ✅ | `RutFormat`, canónico `12345678-5`, índice único. |
| Nombre y apellido separados | ❌ | Un solo campo `Name`. |
| Correo normalizado y único | ✅ | `Trim().ToLowerInvariant()` + índice único. |
| Estado invitada / activa / deshabilitada | 🟡 | Solo `Enabled` (bool). |
| Fechas de creación/actualización y responsable | ❌ | No en `User`. |
| Hash adaptativo y migración del SHA-256 | ✅ | PBKDF2 con re-hash transparente. |
| Activación por correo, recuperación, tokens de un uso, outbox | ❌ | El admin fija la contraseña al registrar. |

## §4 Horarios, sesiones y aforo
| Requisito | Estado | Situación actual |
|---|---|---|
| Aforo físico de 30 personas | ❌ | Solo capacidad por bloque (semilla 20); no se controla el aforo de la sala. |
| Reserva con fecha concreta e identificable | 🟡 | `ScheduleSlot` semanal + `Date`; no hay entidad `Session` con estado. |
| Duración de bloque configurable | 🟡 | Definida por cada `ScheduleSlot`; la UI asume 90 min. |
| Último cupo protegido con locking | ✅ | `SELECT … FOR UPDATE`, 409; prueba concurrente. |
| Única reserva activa por usuario/sesión | ✅ | Índice único filtrado `Status = 'Reservado'`. |
| Única visita abierta por persona | ❌ | Una asistencia por usuario y día (`UserId, Date` único). |
| No confiar en datos del navegador | ✅ | Identidad y rol desde la sesión; capacidad desde BD. |

## §5 Reservas individuales y prioridad
| Requisito | Estado | Situación actual |
|---|---|---|
| Prioridad Deportista → … → Externo | ❌ | Sin categorías ni mecanismo (pendiente de decisión §18.1). |
| Deportistas: varias sesiones por día | 🟡 | Cualquiera puede reservar varios bloques el mismo día. |
| Estudiantes: máximo una por día | ❌ | Sin límite diario. |
| Límites semanales por plan | ❌ | Planes pendientes de definición (correcto no inventarlos). |

## §6 Reservas grupales por rama
| Requisito | Estado | Situación actual |
|---|---|---|
| `GroupBooking` nominativo, ≥ 60 min, todo o nada, idempotente | ❌ | No existe. |

## §7 Entrada, salida y reingreso
| Requisito | Estado | Situación actual |
|---|---|---|
| Entrada desde 15 min antes | ✅ | `ScheduleWindow.Tolerance` = 15 min. |
| Sin ingreso después del fin | ❌ | La ventana acepta hasta **15 min después** del fin (lo que §7 pide no asumir). |
| Entradas/salidas ilimitadas, una fila por visita | ❌ | Una entrada y una salida por día; no hay reingreso. |
| Salida permitida a cuenta deshabilitada que está dentro | ❌ | `RegisterExitAsync` rechaza al usuario deshabilitado (caso §17.17). |
| Registrar tipo de documento revisado, operador y motivo de rechazo | 🟡 | Se guarda el RUT del operador; no el documento ni los rechazos. |

## §8 QR e identificación
| Requisito | Estado | Situación actual |
|---|---|---|
| Token firmado, no reutilizable fuera de la sesión | 🟡 | HMAC con clave propia, válido **todo el día**, no ligado a una reserva/sesión. |
| Validar y luego confirmar con documento (dos pasos) | ❌ | Escanear registra la entrada directamente. |
| Pantalla con nombre, RUT, categoría, rama y sesión | 🟡 | `/qr/my-code` da nombre y RUT; faltan categoría, rama y sesión. |

## §9 Inasistencias y alertas
| Requisito | Estado | Situación actual |
|---|---|---|
| Fecha de creación de la reserva | ✅ | `CreatedAt` UTC. |
| `NoShow`, job idempotente, alertas con acuse | ❌ | No existe. |

## §10–§12 Administración, KPIs y Excel
| Requisito | Estado | Situación actual |
|---|---|---|
| Filtros (período, rama, categoría, estado, usuario) | ❌ | `GET /reservations` devuelve todo sin filtros ni paginación. |
| KPIs con definición y denominador; aforo global | ❌ | Sin endpoint de métricas (el frontend calcula lo mínimo con datos reales). |
| Exportación `.xlsx` y su auditoría | ❌ | No existe. |
| Importación con vista previa e invitaciones | ❌ | No existe. |

## §13–§14 Entidades y contratos
Faltan `SportBranch`, `Plan`, `Session`, `GroupBooking`, `Visit`, `AttendanceAlert`, `Invitation`, `AuditEvent` y los jobs.
Las rutas actuales (`/api/v1/auth/...`, `/reservations/user/{id}`, `/attendance/entry`) difieren de las propuestas; el documento
pide acordarlas antes de conectar React. La API ya está versionada, así que pueden introducirse sin romper la versión 1.

## §16 Seguridad y operación
| Requisito | Estado | Situación actual |
|---|---|---|
| Permisos e identidad en servidor | ✅ | JWT en cookie HttpOnly, CSRF, deshabilitados → 401. |
| `America/Santiago`, instantes UTC, reloj del servidor | ✅ | `IGymClock` + `TimeProvider`. |
| Migraciones versionadas | ✅ | EF Core; faltan procedimiento de respaldo y rollback. |
| Índices únicos, FKs y transacciones | ✅ | En reservas y asistencias. |
| Secretos fuera del repositorio | ✅ | Variables de entorno validadas al arrancar. |
| Rate limiting en login | ✅ | 5/min por IP. |
| Rate limiting en recuperación, QR y acceso | ❌ | Solo login. |
| Auditoría de operaciones administrativas | ❌ | Solo logs con correlación. |
| `Idempotency-Key` | ❌ | No existe. |

## §17 Casos de aceptación
| # | Estado | # | Estado | # | Estado | # | Estado |
|---|---|---|---|---|---|---|---|
| 1 | ✅ | 8 | ❌ | 15 | ❌ | 22 | ❌ |
| 2 | ❌ | 9 | ❌ | 16 | 🟡 manipulado/vencido ✅; por reserva n/a | 23 | ❌ |
| 3 | ❌ | 10 | ✅ | 17 | ❌ | 24 | ❌ |
| 4 | ✅ | 11 | ❌ (+15 min) | 18 | ❌ | 25 | ❌ |
| 5 | 🟡 sin categoría | 12 | ❌ | 19 | ❌ | 26 | ❌ |
| 6 | ❌ | 13 | ❌ | 20 | ❌ | 27 | ❌ |
| 7 | ❌ (planes pendientes) | 14 | ❌ | 21 | ❌ | 28 | ❌ |

Total: 3 ✅, 3 🟡, 22 ❌.

## Propuesta de siguiente fase (requiere decisiones de §18)
1. **F7 – Modelo de identidad:** roles con Recepcionista, categorías, ramas, nombre/apellido, estados de cuenta, auditoría.
2. **F8 – Sesiones y visitas:** `Session`, aforo físico 30, visitas con reingreso, QR por reserva, validación + confirmación,
   ventana `[inicio − 15, fin)`, salida siempre permitida.
3. **F9 – Reglas de reserva:** límite diario de estudiantes, grupos de rama, `Idempotency-Key`.
4. **F10 – Cuentas por correo:** invitaciones, recuperación, outbox (requiere proveedor de correo y dominio).
5. **F11 – Inasistencias, KPIs y Excel.**

Bloqueantes de decisión: mecanismo de prioridad, tolerancia al fin de sesión, una o varias ramas, grupos nominativos,
autenticación por rol y proveedor de correo.

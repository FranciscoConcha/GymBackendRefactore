# Brechas frente a «UCN Gym: requerimientos de backend» (2026-11-18)

Estado tras implementar F7–F11 (rama `feat/requerimientos-2026-11-18`), según el contrato
`docs/CONTRATO_REQUERIMIENTOS_2026-11.md` y las decisiones del usuario. Cada estado se comprobó en el código y en pruebas que se
ejecutaron: backend con 208 unitarias y 245 de integración contra PostgreSQL real, con migraciones y semilla.

> La versión anterior de este documento (contraste con `v1.0.0`, commit `f86aca2`: 5 ✅, 3 🟡, 20 ❌) está en el historial de git.

Leyenda: ✅ cumple · 🟡 parcial · ❌ no cumple · ⏸ pendiente de definición del cliente

## Decisiones del usuario que adaptan el documento
- **Prioridad por ventanas de anticipación por categoría**, configurables en `Reservations:CategoryMaxDaysAhead`. Los deportistas
  no tienen límite de reservas; los estudiantes, 1 bloque por día; no hay más límites.
- **Grupos por rama y no nominativos**, creados por administradores. Dos modos: una cantidad de cupos o todo el cupo disponible.
  Se crean con 60 minutos o más de anticipación.
- **Una rama por persona**. Cada reserva guarda una copia de la categoría y la rama.
- **QR por reserva**, válido solo durante su bloque `[inicio − 15 min, fin)`. Lo validan administración y recepción en dos pasos.
- Las altas manuales usan una **contraseña temporal** con cambio obligatorio. Los usuarios importados o creados sin contraseña
  reciben un **correo de activación** (Brevo, SMTP).
- **Excel en el servidor** con ClosedXML. La importación solo da de alta; las cuentas existentes se omiten.

## §2 Roles y categorías
| Requisito | Estado | Implementación |
|---|---|---|
| Roles Administrador / Recepcionista / Usuario | ✅ | `Roles` con matriz de autorización (contrato §9). El personal inicia sesión por nombre de usuario. |
| Autorización por endpoint y campos limitados por rol | ✅ | La recepción solo valida, registra entradas y salidas y ve el estado actual; el resto → 403 (caso 3). |
| Categorías separadas del rol | ✅ | Deportista, Estudiante, Funcionario y Externo. |
| Rama obligatoria para deportistas | ✅ | `SportBranch` (CRUD de administración). Una rama con referencias no se puede borrar. |

## §3 Identidad y cuentas
| Requisito | Estado | Implementación |
|---|---|---|
| Id estable, RUT con DV y `K`, correo único | ✅ | Sin cambios respecto de `v1.0.0`. |
| Nombre y apellido separados | ✅ | `FirstName` y `LastName`. La migración `F7Identity` convierte `Name`. |
| Estado de la cuenta | ✅ | `AccountStatus` (Pendiente/Activa) + `Enabled`. |
| Fechas de creación y actualización, y auditoría | ✅ | `CreatedAt`/`UpdatedAt` y `AuditEvent` en las operaciones de administración. |
| Activación, recuperación, tokens de un uso y outbox | ✅ | `AccountToken` con hash SHA-256 y `EmailOutbox` + `BackgroundService` (Smtp/Pickup). La recuperación responde siempre 202 genérico (casos 22 y 23). |
| Contraseña temporal y revocación de sesiones | ✅ | `MustChangePassword` + `SessionVersion` (claim `sv`). |

## §4 Horarios, sesiones y aforo
| Requisito | Estado | Implementación |
|---|---|---|
| Aforo físico de 30 personas | ✅ | `Gym:MaxOccupancy` = 30, con `pg_advisory_xact_lock` en la entrada (caso 14). |
| Reserva con fecha concreta | ✅ | `ScheduleSlot` semanal + `Date`. No se creó una entidad `Session` aparte (no hizo falta). |
| Duración de bloque | ✅ | La define cada bloque (90 minutos en la semilla). |
| Último cupo protegido; los cupos de grupos cuentan | ✅ | Bloqueo `FOR UPDATE` del bloque, compartido por reservas y grupos (casos 1 y 2). |
| Una visita abierta por persona | ✅ | Índice único parcial `ExitAt IS NULL`. |

## §5 Reservas individuales y prioridad
| Requisito | Estado | Implementación |
|---|---|---|
| Prioridad por categoría | ✅ | Ventanas de anticipación por categoría (decisión del usuario). |
| Deportistas: varios bloques por día | ✅ | Caso 5. |
| Estudiantes: máximo uno por día | ✅ | Caso 6. |
| Límites semanales por plan | ⏸ | Los planes no están definidos (caso 7). No se inventaron. |
| Inasistencia | ✅ | Estado `Inasistencia` asignado por el job de §9. |

## §6 Reservas grupales por rama
| Requisito | Estado | Implementación |
|---|---|---|
| Grupo con 60 minutos o más, todo o nada e idempotente | ✅ | `GroupBooking` en modos `Cupos`/`TotalDisponible` y `Idempotency-Key` obligatoria (casos 2, 8 y 28). |
| Uso solo por deportistas de la rama | ✅ | `usedSeats` cuenta personas distintas; otra rama → rechazo (caso 9, adaptado). |

## §7 Entrada, salida y reingreso
| Requisito | Estado | Implementación |
|---|---|---|
| Ventana `[inicio − 15 min, fin)` | ✅ | Casos 10 y 11, con bordes exactos. |
| Reingresos ilimitados, una fila por visita | ✅ | `Visit`. Una salida temporal no libera el cupo (casos 12 y 13). |
| Salida de una cuenta deshabilitada que está dentro | ✅ | Salida por `userId` desde la lista de personas presentes (caso 17). |
| Registro del documento revisado, el operador y los rechazos | ✅ | `Visit.DocumentType` y `AccessEvent` para cada intento, aceptado o rechazado. |

## §8 QR e identificación
| Requisito | Estado | Implementación |
|---|---|---|
| Token firmado por reserva, válido solo en su bloque | ✅ | HMAC-SHA256 v2 `{k, rid/gid, uid, nbf, exp}` con comparación en tiempo constante. Un QR de otra reserva, vencido o manipulado → rechazo (caso 16). |
| Validar y después confirmar con el documento | ✅ | `validate` no registra la entrada; `entry` exige `documentType` (caso 15). |
| Pantalla con nombre, RUT, categoría, rama y sesión | ✅ | `GET /me/reservations/{id}/qr` y `/me/group-bookings/{id}/qr`. |

## §9 Inasistencias y alertas
| Requisito | Estado | Implementación |
|---|---|---|
| Job idempotente y alertas con acuse | ✅ | `AttendanceAlert` única por reserva. Las cancelaciones no cuentan y una visita breve cuenta como asistencia. Leer la alerta no borra la inasistencia (casos 18–21). |

## §10–§12 Administración, KPIs y Excel
| Requisito | Estado | Implementación |
|---|---|---|
| Filtros por período, rama, categoría, estado y usuario | ✅ | En usuarios, visitas, alertas, métricas y exportación. |
| KPIs con definición y aforo global | ✅ | `GET /admin/metrics`, agregados en la BD. Los filtros usan la copia de la reserva (caso 27). |
| Exportación `.xlsx` auditada y sin secretos | ✅ | Fórmulas neutralizadas, hoja `Filtros` y auditoría `export.created` (caso 26). |
| Importación con vista previa e invitaciones | ✅ | La vista previa no tiene efectos; la confirmación es transaccional e idempotente (casos 24 y 25). |

## §16 Seguridad y operación
| Requisito | Estado | Implementación |
|---|---|---|
| Permisos e identidad en el servidor, CSRF y cookie HttpOnly | ✅ | El rol y el estado se releen de la BD en cada petición. |
| Rate limiting en login, recuperación y acceso | ✅ | Política `access` por operador. Recuperación limitada a 3 por hora por cuenta. |
| Auditoría de operaciones administrativas | ✅ | `AuditEvent`. |
| `Idempotency-Key` | ✅ | `IdempotencyRecord`: 422 si la misma clave llega con otro cuerpo; expira a las 24 horas. |
| Migraciones versionadas | ✅ | `F7Identity`, `F7PasswordAndBookingSnapshot` y `RequerimientosNov2026`. Elimina la tabla `Attendances` (sin datos que conservar). |

## §17 Casos de aceptación
| # | Estado | # | Estado | # | Estado | # | Estado |
|---|---|---|---|---|---|---|---|
| 1 | ✅ | 8 | ✅ | 15 | ✅ | 22 | ✅ |
| 2 | ✅ | 9 | ✅ adaptado | 16 | ✅ | 23 | ✅ |
| 3 | ✅ | 10 | ✅ | 17 | ✅ | 24 | ✅ |
| 4 | ✅ | 11 | ✅ | 18 | ✅ | 25 | ✅ |
| 5 | ✅ | 12 | ✅ | 19 | ✅ | 26 | ✅ |
| 6 | ✅ | 13 | ✅ | 20 | ✅ | 27 | ✅ |
| 7 | ⏸ planes sin definir | 14 | ✅ | 21 | ✅ | 28 | ✅ |

Total: 27 ✅ y 1 ⏸ (caso 7, pendiente de que el cliente defina los planes).

## Pendientes
- **Planes y límites semanales (caso 7):** a la espera de la definición del cliente.
- **Correo real:** completar en el `.env` las credenciales SMTP de Brevo y confirmar el remitente (`no-replay@` o `no-reply@agencia180.cl`).

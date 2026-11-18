# ADR 0003: Migraciones y esquema

## Contexto
El esquema persistente debe evolucionar de forma versionada y proteger las reglas de integridad y capacidad bajo concurrencia.

## Decisión
Versionar el esquema con EF Core (`InitialCreate` y `WidenRutForCheckDigit`). Las reservas se relacionan con un bloque y una fecha concreta. La BD aplica claves foráneas, índices únicos y restricciones de capacidad y horario. Para reservar, bloquear la fila del bloque con `FOR UPDATE` dentro de una transacción; las violaciones de unicidad se responden como 409.

## Consecuencias
El cambio de esquema se aplica con migraciones. La fecha de reserva participa en la identidad y las restricciones de integridad se hacen cumplir también en PostgreSQL.

# ADR 0004: Zona horaria

## Contexto
Las fechas y ventanas operativas del gimnasio deben calcularse con una zona explícita, independientemente de la zona del servidor.

## Decisión
Configurar `America/Santiago` mediante `Gym:TimeZone` y acceder al tiempo del gimnasio a través de `IGymClock`. Guardar instantes en UTC en la base de datos; convertirlos a hora local para reglas de negocio.

## Consecuencias
La zona puede configurarse y el reloj puede sustituirse en pruebas. Las fechas de operación siguen el calendario local del gimnasio.

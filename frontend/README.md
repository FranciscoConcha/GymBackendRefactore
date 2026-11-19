# UCN Gym

Frontend React + TypeScript conectado a la API F7–F11 mediante `/api/v1` en el mismo origen. El contrato es `../docs/CONTRATO_REQUERIMIENTOS_2026-11.md`.

## Desarrollo

Node.js 22.12 o superior. Con las dependencias instaladas:

```sh
npm run dev
npm run build
npm test
npx prettier --write src tests
```

Vite conserva el proxy `/api` a `http://localhost:5008`. Nginx conserva `/api/` en el mismo origen y la CSP existente. No hay URL de API configurable en las pantallas de cuenta.

Se eliminaron `read-excel-file` y `write-excel-file` de `package.json`. El orquestador debe regenerar `package-lock.json` antes de usar `npm ci`; este encargo no modifica el lock ni instala paquetes.

## Sesión y operaciones

- Login de usuarios por RUT; personal por nombre de usuario. La sesión se restaura con `/auth/me` y su rol determina el portal. Un 401 global vuelve al login.
- Una contraseña temporal bloquea todos los destinos hasta completar `/auth/change-password`. También se reconoce el 403 que exige ese cambio.
- Las cookies se envían con `credentials: same-origin`; toda mutación, incluidas subidas y exportaciones, envía `X-Gym-Csrf: 1`.
- Los datos y tokens solo viven en memoria. No se persisten datos de negocio, sesión ni tokens en almacenamiento del navegador.
- Los errores muestran el mensaje del servidor, incluidos conflictos de cupo (409) e idempotencia (422). El servidor aplica ventanas por categoría, aforo, permisos y reglas de reserva. El cliente deshabilita bloques ya iniciados según la hora de Santiago.
- Reservas, grupos, entradas y confirmaciones de importación usan una clave de idempotencia por envío. Al reintentar el mismo envío se conserva la clave; cambiar sus datos o comenzar otro envío crea una nueva.

## Pantallas

- `/estudiante/inicio`: horarios reales, reservas, QR por reserva, visitas y grupos de la rama para deportistas. Las inasistencias se consultan al cargar y su ventana confirma la lectura en el servidor al cerrarse.
- `/admin/inicio`: métricas del servidor con período, rama y categoría; el aforo es global. Se muestran demanda por horario/rama y usuarios habilitados por categoría. No se genera una serie diaria que el contrato no proporciona.
- Administración: usuarios (alta con invitación o contraseña temporal, edición, estado, invitación y reset), ramas, reservas, grupos, Excel, alertas y visitas.
- `/recepcion/inicio` y `/recepcion/acceso`: estado actual y acceso, sin opciones administrativas. El estado se consulta al abrir y se puede actualizar explícitamente.
- Acceso: validar un token muestra identidad, sesión y aforo sin registrar entrada. Después se selecciona Carnet, TNE o credencial universitaria y se confirma identidad. La salida selecciona una persona de las visitas abiertas y envía su `userId`, sin QR.
- Grupos: reservas no nominativas por rama, por cantidad de cupos o todo el cupo disponible; listado y cancelación administrativos, QR del grupo para deportistas de la rama.
- Excel: el servidor procesa el archivo multipart, devuelve resumen y filas para vista previa y crea cuentas solo tras confirmar. Las exportaciones usan filtros y el nombre de `Content-Disposition`. No se procesa Excel en el navegador.
- `/activar-cuenta?token=…` y `/restablecer-contrasena?token=…`: token leído en memoria y retirado de la URL mediante `history.replaceState`; se envía `newPassword`. Recuperación siempre muestra el mensaje genérico.
- Cada QR muestra la vigencia entregada por el servidor y avisa si aún no está vigente o ya venció. Su validez y elegibilidad se comprueban en el servidor.

La cámara necesita permiso y un contexto seguro. También se puede escribir el token manualmente. Los gráficos conservan ECharts, carga diferida y alternativas accesibles en tablas.

## Pruebas

`npm test` ejecuta Vitest: cliente HTTP, CSRF, idempotencia, errores, multipart y descargas, adaptadores, zona horaria y vigencia del QR. Los E2E usan `tests/api-fixture.ts`, con reloj fijo a las 06:00 y rutas simuladas del contrato, incluidas restricciones por rol.

Los E2E cubren sesión y redirección por rol, storage vacío, contraseña obligatoria, activación sin token en URL, alertas y acuse, acceso en dos pasos y salida sin QR, grupos e idempotencia, importación con vista previa/confirmación, exportación y métricas. Se escribieron para este encargo pero **no se ejecutaron**, por instrucción del usuario. El chequeo de tipos no sustituye una ejecución del navegador. Tampoco se verificó contra una API real ni se hizo una revisión visual en navegador.

Cuando el orquestador autorice ejecutar E2E:

```sh
npm run test:e2e
```

No se realizaron commits, despliegues ni cambios de infraestructura. Antes de integrar, corresponde al orquestador revisar el diff y ejecutar los E2E y la validación contra el backend real.

## Recursos visuales

Logo PNG facilitado por el responsable del proyecto. Paleta existente: azul marino, dorado y blanco. Fotografía ilustrativa de [Unsplash](https://images.unsplash.com/photo-1534438327276-14e5300c3a48); no corresponde al recinto UCN.

## Resultado de verificación del encargo

- Compilación: `npm run build` completada. Vite avisa que el módulo de gráficos supera 500 kB; no es un error de compilación.
- Vitest: 31 pruebas aprobadas en 6 archivos, incluidas pruebas de componentes para activación, contraseña temporal y acceso.
- Formato aplicado con Prettier sobre `src` y `tests`.
- Búsqueda de APIs de almacenamiento y símbolos del modo demo en `src`: sin coincidencias. Sin cambios en infraestructura ni en `package-lock.json`.
- Chequeo de tipos adicional de los E2E: no completado. El primer intento indicó `TS5112` (la CLI requiere `--ignoreConfig` al pasar archivos); al ajustar esa opción indicó `TS2688: Cannot find type definition file for 'node'`. No se instalaron dependencias para resolverlo.
- Playwright, comprobación visual, backend real y revisión independiente: no ejecutados. La revisión fue propia, sin delegación, conforme a las instrucciones del encargo.

# UCN Gym

Frontend React + TypeScript para reservas y acceso a la sala de pesas.

**Versión de presentación.** Datos ficticios en localStorage, sin API ni autenticación real. Los perfiles seleccionables y los QR no autorizan acceso al recinto. No ingresar datos personales reales. Las reglas confirmadas se representan en el cliente; el backend debe hacer cumplir permisos, cupos, identidad y tiempos de forma autoritativa.

## Desarrollo

Node.js 22.12 o superior.

```sh
npm ci
npm run dev
```

## Validación

```sh
npm test
npm run build
npx playwright install chromium
npm run test:e2e
```

## Despliegue de la demo

```sh
docker compose up --build -d
```

Disponible en `http://localhost:8080`; salud en `/healthz`. Terminar HTTPS en el proxy del alojamiento para usar la cámara fuera de localhost. Alternativamente, publicar `dist/` después de `npm run build` en un servidor estático.

El contenedor ejecuta Nginx sin privilegios. Los archivos generados, dependencias instaladas, secretos e informes locales no se versionan. Las variables `VITE_*` son públicas: nunca usarlas para secretos.

Para ejecutar las pruebas de navegador contra el contenedor levantado: `E2E_BASE_URL=http://localhost:8080 npm run test:e2e`.

## Recorrido

Portales independientes:

- `/estudiante/inicio`: horarios, reservas personales, QR y perfil.
- `/admin/inicio`: indicadores, control de acceso, usuarios y reservas globales.
- `/recepcion/inicio`: aforo y personas actualmente dentro; `/recepcion/acceso` permite escaneo e identificación, sin historial ni exportaciones.
- `/admin/grupos`: sesiones de una rama, con participantes nominativos y al menos una hora de anticipación.
- `/admin/archivos`: importación de usuarios con validación previa y exportación `.xlsx` de los registros locales.

Recorrido: reservar desde el portal usuario → mostrar QR con nombre y RUT → validar Carnet/TNE/credencial → registrar entrada/salida en recepción → reingresar mientras siga vigente la sesión → consultar el historial propio.

Los datos persisten por navegador; no se sincronizan entre dispositivos. La cámara requiere permiso y un contexto seguro. También se puede ingresar el código manualmente. La separación de portales es de interfaz, no sustituye autenticación ni autorización del servidor.

El panel filtra por rama y período: reservas, asistencia, cancelaciones, inasistencias, demanda y permanencia sumando visitas cerradas. El aforo permanece global para no ocultar ocupación de otras ramas. La capacidad es de **30 personas**.

Deportistas: rama obligatoria y varias reservas diarias. Estudiantes: una por día; solo se aplica una cuota semanal si está explícitamente configurada. La prioridad Deportista → Estudiante → Funcionario → Externo está documentada; falta acordar si se aplicará por ventanas, cupos protegidos o lista de espera. No se desplazan reservas confirmadas.

El ingreso se permite desde 15 minutos antes del inicio y hasta antes del fin, sin penalizar atrasos durante la sesión. Las salidas temporales no liberan la reserva. Cada reingreso conserva las visitas anteriores; se puede registrar salida tras finalizar el horario o deshabilitar la cuenta.

Al finalizar una sesión sin entrada se registra inasistencia y se muestra un aviso al usuario. No se aplican sanciones automáticas mientras no se apruebe la política.

Las cuentas nuevas/importadas quedan pendientes de activación. Las páginas `/activar-cuenta`, `/recuperar-contrasena` y `/restablecer-contrasena` están preparadas para los endpoints de correo/contraseñas, usando `VITE_API_BASE_URL` (por defecto `/api`). No simulan correos enviados cuando la API no está disponible. Configurar proveedor, tokens, dominio de enlaces y proxy/CORS en la integración real.

Gráficos con Apache ECharts: tooltips, series activables y zoom en períodos de 30 días. Incluyen tablas alternativas y controles por teclado. El motor gráfico se carga bajo demanda únicamente en el panel administrativo.

Backend independiente: https://github.com/xAchaoooo/Back_Gym. Esta interfaz aún no consume esa API.

## Recursos visuales

Logo PNG facilitado por el responsable del proyecto. Paleta: azul marino, dorado y blanco. Fotografía ilustrativa de [Unsplash](https://images.unsplash.com/photo-1534438327276-14e5300c3a48); no corresponde al recinto UCN.

Para uso real faltan API segura, autenticación, reglas aprobadas y validación de infraestructura. Esta demo no sustituye el sistema institucional.

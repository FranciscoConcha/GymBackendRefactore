# UCN Gym

Frontend React + TypeScript para reservas y acceso a la sala de pesas.

**Versión de demostración.** Datos ficticios en localStorage, sin API ni autenticación real. Los perfiles seleccionables y los QR no autorizan acceso al recinto. No ingresar datos personales reales. Las fechas, cupos, horarios y reglas de acceso son ejemplos pendientes de validación.

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

Recorrido: reservar desde el portal estudiante → mostrar QR → registrar entrada/salida en `/admin/acceso` → consultar el historial en `/estudiante/reservas`.

Los datos persisten por navegador; no se sincronizan entre dispositivos. La cámara requiere permiso y un contexto seguro. También se puede ingresar el código manualmente. La separación de portales es de interfaz, no sustituye autenticación ni autorización del servidor.

El panel calcula reservas por fecha de sesión, presencia actual, asistencia registrada, cancelaciones, demanda por bloque y permanencia media desde los registros disponibles. Sin registros, muestra cero o un guion; no genera tendencias artificiales. La capacidad configurada es de 20 personas y requiere validación operativa.

Gráficos con Apache ECharts: tooltips, series activables y zoom en períodos de 30 días. Incluyen tablas alternativas y controles por teclado. El motor gráfico se carga bajo demanda únicamente en el panel administrativo.

Backend independiente: https://github.com/xAchaoooo/Back_Gym. Esta interfaz aún no consume esa API.

## Recursos visuales

Logo PNG facilitado por el responsable del proyecto. Paleta: azul marino, dorado y blanco. Fotografía ilustrativa de [Unsplash](https://images.unsplash.com/photo-1534438327276-14e5300c3a48); no corresponde al recinto UCN.

Para uso real faltan API segura, autenticación, reglas aprobadas y validación de infraestructura. Esta demo no sustituye el sistema institucional.

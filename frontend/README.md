# UCN Gym

Frontend React 19 + TypeScript para reservas y control de acceso a la sala de pesas. Consume exclusivamente la API `/api/v1`; los datos de negocio y la sesión permanecen en el servidor. No utiliza localStorage ni sessionStorage.

## Desarrollo con la API

Requiere Node.js 22.12 o superior y la API ejecutándose en `http://localhost:5008`. Con las dependencias del proyecto disponibles:

```sh
npm.cmd run dev
```

Vite reenvía `/api` a ese servidor. En producción, el frontend y la API deben publicarse en el mismo origen, con `/api/` reenviado al backend. La sesión usa una cookie HttpOnly del servidor; todas las peticiones incluyen `credentials: "same-origin"` y `Accept: application/json`. Las mutaciones incluyen `X-Gym-Csrf: 1`. No se configuran tokens ni secretos en variables `VITE_*`.

El login permite elegir Usuario (RUT y contraseña) o Administración (usuario y contraseña). `/auth/me` restaura la sesión y determina el portal según el rol del servidor: `/estudiante/inicio` para Cliente y `/admin/inicio` para Admin. Una URL de otro portal redirige al correspondiente. Un 401 durante el uso termina la sesión de interfaz y muestra el login; el cierre de sesión invoca `/auth/logout`.

## Funciones y datos

- Horarios por día y capacidad recibida desde la API, reservas personales y cancelación confirmada.
- QR personal del día: representa el token de `/qr/my-code`, independiente de una reserva concreta. Recepción registra entrada y salida con ese código. La cámara requiere permisos y un contexto seguro; también admite ingreso manual.
- Administración de usuarios: registro y habilitación de acceso.
- Indicadores y gráficos calculados desde reservas reales, por fecha de sesión. Los porcentajes sin denominador muestran un guion y la demanda sin reservas muestra un estado vacío.

La API no proporciona ocupación actual ni historial de asistencias; esas secciones muestran su indisponibilidad. Las reservas utilizan únicamente `Reservado`, `Cancelado` y `Asistió`. No se inventan entradas, salidas ni series históricas de asistencia.

## Verificaciones

En PowerShell se utilizan los ejecutables `.cmd` para evitar el bloqueo de `npm.ps1`:

```sh
npm.cmd test
npm.cmd run format:check
npm.cmd run build
npx.cmd playwright test
```

Playwright ejecuta los proyectos desktop y mobile con Chromium. Sus pruebas interceptan `**/api/v1/**` mediante `page.route`, mantienen el estado de prueba en memoria y comprueban CSRF en cada mutación. No requieren una API activa. Incluyen sesión, errores, redirección por rol, reserva, QR, acceso, cancelación, usuarios, accesibilidad axe, navegación, gráficos y almacenamiento vacío. Para un servidor frontend ya iniciado se puede definir `E2E_BASE_URL`; la API sigue simulada por las pruebas.

Para corregir formato:

```sh
npx.cmd prettier --write src tests vite.config.ts playwright.config.ts
```

La compilación produce `dist/`. Las pruebas con API simulada verifican el contrato de interfaz; la integración con un backend desplegado se valida en su entorno correspondiente.

## Recursos visuales

Logo PNG facilitado por el responsable del proyecto. Paleta: azul marino, dorado y blanco. Fotografía ilustrativa de [Unsplash](https://images.unsplash.com/photo-1534438327276-14e5300c3a48); no corresponde al recinto UCN.

# ADR 0005: Tokens y claves

## Contexto
Los tokens de autenticación y los códigos QR tienen propósitos distintos y deben validarse bajo políticas explícitas.

## Decisión
Firmar JWT con HS256 y validar issuer y audience. Usar `Qr:SigningKey` distinta de `Jwt:Key`. En `OnTokenValidated`, comprobar que el usuario exista y siga habilitado. Aplicar rate limiting a los endpoints de login. Publicar controladores bajo `/api/v1`.

## Consecuencias
Un usuario deshabilitado no conserva acceso mediante un JWT previo. Los intentos de login sobre el límite reciben 429 y las rutas antiguas `/api/...` no se mapean.

## Sesión HTTP y CSRF

El JWT de sesión no se expone a JavaScript: login y admin-login lo entregan exclusivamente en `__Host-gym_session`, con `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/`, sin `Domain` y expiración sincronizada con el JWT de 8 horas. El contrato público contiene `message`, `user` y `expiresAt`. El servicio devuelve un resultado interno y el controlador escribe la cookie.

JwtBearer toma la cookie solo en ausencia de `Authorization`, conserva la comprobación de usuarios deshabilitados y nunca acepta tokens en query string. `GET /api/v1/auth/me` consulta el usuario actual en BD. `POST /api/v1/auth/logout` elimina la cookie; no revoca otras copias del JWT.

El middleware CSRF se ejecuta tras CORS y antes de autenticación: exige `X-Gym-Csrf: 1` en POST, PUT, PATCH y DELETE bajo `/api/`, incluidos logins y logout, salvo solicitudes con `Authorization`. Si hay `Origin`, se contrasta con los orígenes permitidos. CORS permite credenciales con orígenes explícitos y valida al arrancar que no exista `*`. El frontend usa HTTPS, `credentials: 'include'` y la cabecera CSRF; `SameSite=Strict` requiere despliegue en el mismo sitio.

# ADR 0005: Tokens y claves

## Contexto
Los tokens de autenticación y los códigos QR tienen propósitos distintos y deben validarse bajo políticas explícitas.

## Decisión
Firmar JWT con HS256 y validar issuer y audience. Usar `Qr:SigningKey` distinta de `Jwt:Key`. En `OnTokenValidated`, comprobar que el usuario exista y siga habilitado. Aplicar rate limiting a los endpoints de login. Publicar controladores bajo `/api/v1`.

## Consecuencias
Un usuario deshabilitado no conserva acceso mediante un JWT previo. Los intentos de login sobre el límite reciben 429 y las rutas antiguas `/api/...` no se mapean.

# ADR 0002: Hash de contraseñas

## Contexto
Las cuentas existentes pueden tener hashes SHA-256 heredados, mientras el almacenamiento actual requiere un hash lento con sal.

## Decisión
Usar PBKDF2 mediante `PasswordHasher<User>`. Cuando una contraseña coincide con el hash SHA-256 heredado, reemplazarlo transparentemente por el hash actual durante el inicio de sesión. Si no existe una cuenta candidata, verificar una contraseña contra un hash ficticio.

## Consecuencias
Las cuentas se migran al autenticarse correctamente y no requieren restablecimiento inmediato. La verificación ficticia reduce diferencias de tiempo que podrían facilitar la enumeración de cuentas.

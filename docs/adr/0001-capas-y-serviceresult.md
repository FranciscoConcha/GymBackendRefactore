# ADR 0001: Capas y ServiceResult

## Contexto
La lógica de aplicación debe poder ejecutarse fuera de los controladores HTTP y devolver errores de negocio de forma explícita.

## Decisión
Los controladores son delgados y delegan en servicios bajo `Application/<Área>`. Los casos de uso devuelven `ServiceResult<T>` para éxito o error tipado. `ServiceResultExtensions` traduce el resultado a respuestas HTTP.

## Consecuencias
La aplicación concentra reglas de negocio; la extensión mantiene la traducción HTTP en la capa web. Los controladores coordinan la entrada y la respuesta.

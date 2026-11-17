# Flujo de trabajo

## Ramas

```
main  ←  test  ←  dev  ←  <tipo>/<fase>-<descripcion>
```

- `main`: versión estable. Solo recibe PR desde `test`; cada fusión se etiqueta `vX.Y.Z`.
- `test`: candidata de versión/QA. Solo recibe PR desde `dev` (o `fix/*` urgentes, que luego se fusionan también en `dev`).
- `dev`: integración. Recibe PR de ramas de tarea.
- Ramas de tarea: se crean desde `dev`. Tipos: `feat`, `fix`, `refactor`, `tests`, `chore`, `docs` (se usa `tests/` porque `test` es una rama protegida y Git no admite `test/...`). Ejemplo: `refactor/f2-options-config`.

Las tres ramas principales están protegidas: sin push directo, PR obligatorio y CI (`build`) en verde.

## Fusiones

- Tarea → `dev`: *squash merge*.
- `dev` → `test` y `test` → `main`: *merge commit* para conservar trazabilidad.

## Commits

[Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `refactor:`, `test:`, `chore:`, `docs:`.

## Antes de abrir un PR

```sh
dotnet build -c Release
dotnet test
```

No versionar `.env`, secretos ni datos reales. El plan vigente está en `docs/PLAN_REFACTORIZACION.md`.

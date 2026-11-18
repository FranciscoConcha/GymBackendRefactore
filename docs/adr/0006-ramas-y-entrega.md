# ADR 0006: Ramas y entrega

## Contexto
La guía de contribución define ramas protegidas, revisiones por PR y promoción entre entornos. El plan de refactorización establece hitos de entrega.

## Decisión
Usar `dev` para integración, `test` para QA y `main` para versiones estables. Las ramas de tarea usan prefijos `feat/`, `fix/`, `refactor/`, `tests/`, `chore/` o `docs/`. Exigir PR con CI verde y promover `dev` a `test` y luego `main` por hitos H7 y H8.

## Consecuencias
El trabajo se revisa antes de integrarse; las promociones conservan los hitos de QA y entrega establecidos en [CONTRIBUTING.md](../../CONTRIBUTING.md) y [PLAN_REFACTORIZACION.md](../PLAN_REFACTORIZACION.md), §§3 y 5.

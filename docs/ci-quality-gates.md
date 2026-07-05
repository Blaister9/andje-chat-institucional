# CI quality gates

La microfase 05 agrega un workflow de GitHub Actions para validar pull requests
y pushes hacia `develop` y `main`.

## Workflow

Archivo:

```text
.github/workflows/ci.yml
```

Triggers:

- `pull_request` hacia `develop` o `main`.
- `push` hacia `develop` o `main`.

Runner:

- `ubuntu-latest`.

## Validaciones

El job principal valida:

- SDK .NET 8 disponible.
- Node.js 20 disponible.
- PostgreSQL 16 como service container.
- `dotnet restore`.
- `dotnet test backend/Andje.Chat.sln --configuration Release`.
- Build de consola con `npm ci` y `npm run build`.
- Build de widget con `npm ci` y `npm run build`.
- `docker compose config -q`.
- `dotnet list ... package --vulnerable --include-transitive`.
- `npm audit --audit-level=high` para consola y widget.
- `.env` no versionado.

## PostgreSQL en CI

El workflow levanta PostgreSQL 16 con:

```text
POSTGRES_USER=andje
POSTGRES_PASSWORD=andje_dev_local
POSTGRES_DB=andje_chat_test
```

Estos valores son solo dev/test, no secretos institucionales.

El CI define:

```text
ANDJE_DB_PORT=5432
ANDJE_DB_PASSWORD=andje_dev_local
ANDJE_REQUIRE_POSTGRES_TESTS=true
```

Con `ANDJE_REQUIRE_POSTGRES_TESTS=true`, las pruebas PostgreSQL fallan si la DB
no esta disponible. Localmente, si esa variable no existe, esas pruebas pueden
omitirse para no bloquear trabajo sin contenedor.

## Ejecutar localmente

Desde la raiz del repositorio:

```powershell
docker compose up -d db
dotnet test backend/Andje.Chat.sln

cd apps\console
npm ci
npm run build

cd ..\widget
npm ci
npm run build

cd ..\..
docker compose config -q
```

Para simular el comportamiento estricto de CI en pruebas PostgreSQL:

```powershell
$env:ANDJE_REQUIRE_POSTGRES_TESTS="true"
$env:ANDJE_DB_PORT="5433"
dotnet test backend/Andje.Chat.sln
```

## Auditorias

Las auditorias de dependencias se ejecutan y quedan visibles en logs:

- `.NET`: `dotnet list package --vulnerable --include-transitive`
- npm consola/widget: `npm audit --audit-level=high`

Estado inicial de fase 05:

- `.NET` reporta vulnerabilidades transitivas en paquetes del proyecto de
  tests. El comando reporta el hallazgo y no falla el job.
- npm reporta una vulnerabilidad Vite/esbuild que requiere upgrade mayor segun
  `npm audit fix --force`; por eso los pasos npm audit estan temporalmente como
  `continue-on-error: true`.

Los gates bloqueantes son `dotnet test`, builds frontend y `docker compose
config -q`. Una microfase futura debe actualizar dependencias y volver las
auditorias npm bloqueantes.

## Pendientes

- Configurar branch protection en GitHub.
- Marcar el workflow como required check para `develop` y `main`.
- Activar GitHub secret scanning.
- Agregar Gitleaks u otra herramienta dedicada de secret scanning.
- Agregar CI completo que levante todo Docker Compose y haga smoke test HTTP.
- Agregar despliegue solo cuando exista estrategia de ambientes.

## Recomendacion de branch protection

Para `develop` y `main`:

- Requerir pull request.
- Requerir CI verde.
- Bloquear force push.
- Requerir al menos una revision antes de merge.

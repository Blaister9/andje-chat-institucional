# CI quality gates

El workflow de GitHub Actions valida pull requests y pushes hacia `develop` y
`main`. En fase 06 se agrego secret scanning y se endurecieron auditorias.

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
- Gitleaks sobre el arbol de trabajo.
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
docker run --rm -v "${PWD}:/repo" ghcr.io/gitleaks/gitleaks:v8.30.1 detect --source=/repo --no-git --redact --config=/repo/.gitleaks.toml
```

Para simular el comportamiento estricto de CI en pruebas PostgreSQL:

```powershell
$env:ANDJE_REQUIRE_POSTGRES_TESTS="true"
$env:ANDJE_DB_PORT="5433"
dotnet test backend/Andje.Chat.sln
```

## Auditorias

Las auditorias de dependencias son bloqueantes:

- `.NET`: `dotnet list package --vulnerable --include-transitive --format json`
  y validacion del JSON para fallar si aparecen vulnerabilidades.
- npm consola/widget: `npm audit --audit-level=high`

Estado despues de fase 06:

- `.NET` no reporta vulnerabilidades conocidas en los origenes configurados.
- npm no reporta vulnerabilidades high/critical en consola ni widget.
- `npm audit` ya no usa `continue-on-error`.

## Secret scanning

CI ejecuta Gitleaks `v8.30.1` con `.gitleaks.toml`. El scanner es bloqueante:
si encuentra un secreto, el job falla. La configuracion solo permite valores
dev/test documentados (`andje_dev_local`, `andje-agent-local`,
`change-me-local-only`) y `.env.example`.

## Pendientes

- Configurar branch protection en GitHub.
- Marcar el workflow como required check para `develop` y `main`.
- Activar GitHub secret scanning.
- Agregar CI completo que levante todo Docker Compose y haga smoke test HTTP.
- Agregar despliegue solo cuando exista estrategia de ambientes.

## Recomendacion de branch protection

Para `develop` y `main`:

- Requerir pull request.
- Requerir CI verde.
- Bloquear force push.
- Requerir al menos una revision antes de merge.

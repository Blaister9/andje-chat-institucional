# Dependency and secret hardening

Fase 06 reduce deuda de seguridad detectada por CI. Es una mejora tecnica, no
una certificacion de seguridad institucional.

## Hallazgos iniciales

### npm

Consola y widget reportaban:

- `vite <= 6.4.2` dependia de `esbuild <= 0.24.2`.
- Advisory: `GHSA-67mh-4wv8-2f99`.
- `npm audit --audit-level=high` fallaba.
- `npm audit fix --force` proponia salto mayor.

### .NET

El proyecto `Andje.Chat.Api.Tests` reportaba vulnerabilidades transitivas:

- `System.Net.Http 4.3.0`, severidad high.
- `System.Text.RegularExpressions 4.3.0`, severidad high.

El proyecto de API no reportaba vulnerabilidades directas.

## Actualizaciones aplicadas

Frontend:

- Consola: `vite` de `5.4.21` a `8.1.3`.
- Consola: `@vitejs/plugin-react` de `4.7.0` a `6.0.3`.
- Widget: `vite` de `5.4.21` a `8.1.3`.

Backend/test:

- `Microsoft.EntityFrameworkCore.Design` se inspecciono y se mantuvo en
  `8.0.11` para seguir alineado con `Npgsql.EntityFrameworkCore.PostgreSQL
  8.0.11`.
- `Microsoft.AspNetCore.Mvc.Testing` de `8.0.14` a `8.0.22`.
- `Microsoft.AspNetCore.SignalR.Client` de `8.0.14` a `8.0.22`.
- `Microsoft.NET.Test.Sdk` de `17.8.0` a `18.7.0`.
- `xunit` de `2.5.3` a `2.9.3`.
- `xunit.runner.visualstudio` de `2.5.3` a `3.1.5`.
- `Xunit.SkippableFact` de `1.4.13` a `1.5.61`.

Se mantiene `TargetFramework=net8.0`. No se migro a .NET 9/10.

## Estado despues

Comandos esperados:

```powershell
dotnet list backend/Andje.Chat.sln package --vulnerable --include-transitive
dotnet list backend/Andje.Chat.sln package --vulnerable --include-transitive --format json

cd apps\console
npm audit --audit-level=high

cd ..\widget
npm audit --audit-level=high
```

Resultado esperado: sin vulnerabilidades conocidas reportadas por los origenes
configurados. En CI, el reporte .NET en JSON se valida y el job falla si
aparecen entradas con `vulnerabilities`.

## Secret scanning

Se agrego Gitleaks `v8.30.1` al CI:

```bash
docker run --rm \
  -v "${{ github.workspace }}:/repo" \
  ghcr.io/gitleaks/gitleaks:v8.30.1 detect \
  --source=/repo \
  --no-git \
  --redact \
  --config=/repo/.gitleaks.toml
```

Localmente:

```powershell
docker run --rm -v "${PWD}:/repo" ghcr.io/gitleaks/gitleaks:v8.30.1 detect --source=/repo --no-git --redact --config=/repo/.gitleaks.toml
```

## Allowlist

`.gitleaks.toml` permite solo valores dev/test documentados:

- `andje_dev_local`
- `andje-agent-local`
- `change-me-local-only`
- `.env.example`

No se permiten tokens reales, llaves privadas, contrasenas institucionales ni
archivos `.env`.

## No versionar

- `.env`
- secretos reales
- tokens generados
- certificados privados
- dumps de base de datos
- `node_modules`
- `dist`
- `bin`
- `obj`

## Pendientes

- Branch protection en `develop` y `main`.
- GitHub secret scanning nativo.
- Dependabot o Renovate.
- SBOM.
- Escaneo de imagenes de contenedor.
- Smoke test Docker Compose completo en CI.

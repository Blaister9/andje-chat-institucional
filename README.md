# Chat institucional ANDJE

Prototipo MVP de chat institucional para una entidad publica colombiana. Busca
proveer una alternativa controlada a servicios externos tipo tawk.to, con
trazabilidad, privacidad y una ruta futura hacia asistencia con IA controlada.

> Estado: fase 06 - hardening de dependencias y secret scanning. No apto para
> produccion.

## Alcance actual

- Backend .NET 8 Web API + SignalR.
- PostgreSQL 16 con EF Core + Npgsql.
- Widget publico como Web Component TypeScript.
- Consola interna React + Vite + TypeScript.
- Conversaciones persistidas con estados `Pending`, `Active` y `Closed`.
- Reanudacion de conversacion activa en el widget mediante `sessionStorage`.
- Cierre desde consola, notificacion realtime y bloqueo de nuevos mensajes.
- Acceso local de desarrollo para consola de agentes con token opaco temporal.
- Auditoria minima: inicio, mensajes, activacion y cierre.
- CI con pruebas PostgreSQL obligatorias, auditorias de dependencias y Gitleaks.

## Fuera de alcance

- Autenticacion institucional real.
- IA, RAG, resumenes o bots.
- Adjuntos.
- Departamentos, transferencia o asignacion explicita de agente.
- Metricas, dashboard administrativo o reporterias.
- Retencion automatica y anonimizacion automatica.
- Despliegue productivo o alta disponibilidad.

## Ejecutar localmente

Requisitos: Docker Desktop. Para desarrollo directo: .NET SDK 8+ y Node.js 20+.

```powershell
docker compose up --build -d
curl.exe -s http://localhost:8080/health
```

Servicios:

| Servicio | URL |
| --- | --- |
| API health | `http://localhost:8080/health` |
| Hub SignalR | `http://localhost:8080/hubs/chat` |
| Consola | `http://localhost:5173` |
| Widget demo | `http://localhost:5174` |
| PostgreSQL | `localhost:5433`, db `andje_chat` |

La consola local usa el codigo de desarrollo `andje-agent-local` cuando se
ejecuta con Docker Compose. Este valor es solo para demo local; en un entorno
compartido use `.env` y reemplacelo por un valor no versionado.

Detener:

```powershell
docker compose down
```

## Desarrollo local

```powershell
docker compose up -d db
dotnet run --project backend/src/Andje.Chat.Api

cd apps/console
npm install
npm run dev

cd ../widget
npm install
npm run dev
```

## Pruebas y builds

```powershell
docker compose up -d db
dotnet test backend/Andje.Chat.sln

cd apps/console
npm ci
npm run build

cd ../widget
npm ci
npm run build

cd ../..
docker compose config -q
docker run --rm -v "${PWD}:/repo" ghcr.io/gitleaks/gitleaks:v8.30.1 detect --source=/repo --no-git --redact --config=/repo/.gitleaks.toml
```

Las pruebas de persistencia usan PostgreSQL real en `localhost:5433` y la base
`andje_chat_test`. Si la DB no esta disponible, esas pruebas se omiten con
aviso; con `docker compose up -d db` deben ejecutarse.

En CI se define `ANDJE_REQUIRE_POSTGRES_TESTS=true`, por lo que las pruebas de
PostgreSQL fallan si la base no esta disponible. Los valores usados por CI son
dev/test, no secretos institucionales.

## Widget embebible

```html
<script src="https://<host>/andje-chat-widget.js"></script>
<andje-chat-widget titulo="Chat institucional" api-base="https://<api>"></andje-chat-widget>
```

El widget guarda solo el `conversationId` en `sessionStorage` para reanudar una
conversacion activa tras recargar. Esto es conveniencia local, no seguridad.

## Documentacion

- [Modelo de dominio](docs/domain-model.md)
- [Persistencia y auditoria](docs/persistence-audit.md)
- [Prueba manual realtime](docs/manual-test-realtime.md)
- [Fundacion de acceso de consola](docs/security-access-foundation.md)
- [CI y quality gates](docs/ci-quality-gates.md)
- [Dependency and secret hardening](docs/dependency-secret-hardening.md)
- [Linea base de seguridad y privacidad](docs/privacy-security-baseline.md)
- [ADR 0001 - Arquitectura inicial](docs/adr/0001-architecture.md)

## Flujo de trabajo

No se trabaja directamente sobre `main` ni `develop`. Cada microfase entra por
rama `feat/*` y pull request.

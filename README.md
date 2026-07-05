# Chat institucional ANDJE

Prototipo MVP de chat institucional para una entidad pública colombiana,
pensado para reemplazar soluciones externas tipo tawk.to con una alternativa
bajo control institucional: trazabilidad, privacidad, auditoría y preparación
para IA asistida en fases futuras.

> **Estado:** fase 00 — fundación del proyecto. No apto para producción.

## Visión

Un ciudadano que visita el portal de la entidad abre un widget de chat
embebido y conversa en tiempo real con un agente interno, que atiende desde
una consola propia. Toda conversación queda registrada y auditable dentro de
la infraestructura de la entidad, sin depender de servicios de chat de
terceros.

## Alcance del MVP

- Widget de chat embebible en portales de la entidad (Web Component).
- Consola interna para agentes (React).
- Mensajería en tiempo real ciudadano ↔ agente (SignalR).
- Persistencia y trazabilidad de conversaciones (PostgreSQL).
- Auditoría básica de eventos (quién atendió, cuándo, qué pasó).

## Fuera de alcance (por ahora)

- IA asistida (sugerencias de respuesta, resúmenes, bots). La arquitectura la
  contempla, pero no se implementa en el MVP.
- Integración con sistemas externos de la entidad (PQRSD, CRM, mesa de ayuda).
- Autenticación de ciudadanos (el visitante es anónimo con datos mínimos).
- Despliegue a producción, alta disponibilidad y escalamiento horizontal.
- Notificaciones por correo, encuestas de satisfacción, reportería avanzada.

## Arquitectura

Monorepo con tres aplicaciones y una base de datos:

```
andje-chat-institucional/
├── backend/                  # .NET 8 Web API + SignalR
│   ├── Andje.Chat.sln
│   ├── src/Andje.Chat.Api/   # API, hub de chat (/hubs/chat), /health
│   └── tests/Andje.Chat.Api.Tests/  # Smoke tests (xUnit)
├── apps/
│   ├── console/              # Consola de agentes (React + Vite + TS)
│   └── widget/               # Widget embebible (Web Component, Vite + TS)
├── docs/
│   ├── adr/                  # Decisiones de arquitectura
│   ├── domain-model.md       # Modelo de dominio
│   └── privacy-security-baseline.md
├── scripts/                  # Arranque/parada del entorno local
└── docker-compose.yml        # Orquestación local (db + api + frontends)
```

Decisiones clave y su justificación: ver [docs/adr/0001-architecture.md](docs/adr/0001-architecture.md).
Resumen: **React** para la consola (curva de entrada baja, ecosistema amplio,
coherencia con el enfoque de componentes del widget) y **PostgreSQL** como base
de datos (sin costos de licenciamiento para la entidad, primera clase en
Docker, JSONB para metadatos de mensajes).

## Cómo ejecutar localmente

Requisitos: Docker (Desktop en Windows). Para desarrollo fuera de contenedores:
.NET SDK 8+ y Node.js 20+.

### Opción 1 — todo con Docker (comando único)

```bash
docker compose up --build -d
# o, equivalente:
./scripts/dev-up.sh        # Linux/macOS
.\scripts\dev-up.ps1       # Windows
```

Servicios levantados:

| Servicio            | URL                                  |
| ------------------- | ------------------------------------ |
| API — healthcheck   | http://localhost:8080/health         |
| Hub SignalR         | http://localhost:8080/hubs/chat      |
| Consola de agentes  | http://localhost:5173                |
| Demo del widget     | http://localhost:5174                |
| PostgreSQL          | localhost:5432 (db `andje_chat`)     |

Para detener: `docker compose down` (o `scripts/dev-down.*`).

### Opción 2 — desarrollo directo en la máquina

```bash
# Base de datos
docker compose up -d db

# API (puerto 8080)
dotnet run --project backend/src/Andje.Chat.Api

# Consola (puerto 5173)
cd apps/console && npm install && npm run dev

# Widget (puerto 5174, página demo con el widget embebido)
cd apps/widget && npm install && npm run dev
```

### Pruebas

```bash
dotnet test backend/Andje.Chat.sln
```

Los smoke tests verifican que `/health` responde `200 Healthy` y que el hub
SignalR `/hubs/chat` acepta conexiones y responde a `Ping`.

### Compilar los frontends

```bash
cd apps/console && npm run build
cd apps/widget && npm run build   # genera dist/andje-chat-widget.js (IIFE embebible)
```

## Cómo se embebe el widget

El widget es un Web Component autocontenido. Un portal externo lo integra con
dos líneas, sin dependencias de framework:

```html
<script src="https://<host-de-la-entidad>/andje-chat-widget.js"></script>
<andje-chat-widget titulo="Chat institucional"></andje-chat-widget>
```

## Configuración y secretos

- No hay secretos en el repositorio. La única credencial local es la
  contraseña de PostgreSQL de desarrollo, con valor por defecto en
  `docker-compose.yml` y sobreescribible vía `.env` (ver
  [.env.example](.env.example)).
- Los orígenes permitidos por CORS se configuran en
  `backend/src/Andje.Chat.Api/appsettings.json` (`Cors:AllowedOrigins`).

## Documentación

- [ADR 0001 — Arquitectura inicial](docs/adr/0001-architecture.md)
- [Modelo de dominio](docs/domain-model.md)
- [Línea base de seguridad y privacidad](docs/privacy-security-baseline.md)

## Flujo de trabajo

- No se trabaja directamente sobre `main` ni `develop`; todo cambio entra por
  rama `feat/*` y pull request.
- Fase actual: `feat/00-project-foundation`. Siguiente fase sugerida:
  persistencia (EF Core + migraciones) y mensajería básica por SignalR.

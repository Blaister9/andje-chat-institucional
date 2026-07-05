# Persistencia y auditoría — fase 02

Cómo persiste el chat institucional sus datos y qué eventos de auditoría
emite. Complementa a [domain-model.md](domain-model.md).

## Arquitectura de persistencia

- **EF Core 8 + Npgsql** sobre PostgreSQL 16.
- `ChatDbContext` ([backend/src/Andje.Chat.Api/Data/ChatDbContext.cs](../backend/src/Andje.Chat.Api/Data/ChatDbContext.cs)).
- El hub no conoce EF: depende de la abstracción `IConversationStore`.
  - `PostgresConversationStore`: implementación registrada en ejecución normal.
  - `InMemoryConversationStore`: usada por las pruebas del flujo realtime
    (sin base de datos).
- Cada escritura (conversación nueva, mensaje, activación) guarda entidad +
  eventos de auditoría en **un único `SaveChanges`** = una sola transacción.

## Esquema (migración `InitialCreate`)

| Tabla | Columnas | Notas |
| --- | --- | --- |
| `Conversations` | `Id` uuid PK, `VisitorDisplayName` varchar(80) null, `Status` varchar(20), `CreatedAtUtc`, `UpdatedAtUtc`, `ClosedAtUtc` null | `Status`: `Pending` \| `Active` (cierre en fase futura, la columna `ClosedAtUtc` ya existe). Índice por `CreatedAtUtc`. |
| `Messages` | `Id` uuid PK, `ConversationId` uuid FK (cascade), `SenderType` varchar(20), `Body` varchar(2000), `CreatedAtUtc` | Inmutables (solo INSERT). Índice `(ConversationId, CreatedAtUtc)`. |
| `AuditEvents` | `Id` uuid PK, `ConversationId` uuid null, `EventType` varchar(100), `ActorType` varchar(20), `DataJson` jsonb null, `CreatedAtUtc` | Solo INSERT. Índices por `ConversationId` y `CreatedAtUtc`. |

Los timestamps son `timestamptz` y siempre UTC.

## Catálogo de eventos de auditoría

| EventType | Actor | Cuándo se dispara | DataJson |
| --- | --- | --- | --- |
| `conversation.started` | `Visitor` | El visitante inicia una conversación desde el widget | `null` |
| `message.sent.visitor` | `Visitor` | El visitante envía un mensaje | `{"messageId": "…"}` |
| `message.sent.agent` | `Agent` | El agente envía un mensaje | `{"messageId": "…"}` |
| `conversation.activated` | `Agent` | La primera respuesta de un agente pasa la conversación de `Pending` a `Active` | `{"messageId": "…"}` |

**Regla:** `DataJson` lleva referencias (ids), nunca contenido de mensajes ni
datos personales. Los logs de la aplicación tampoco registran contenido.

## Migraciones

- Se aplican **automáticamente al arrancar la API** (`Database:AutoMigrate`,
  `true` por defecto) — así `docker compose up` deja el esquema listo sin
  pasos manuales. Las pruebas del hub lo desactivan.
- Crear una migración nueva (requiere el tool local, ya versionado en
  `backend/dotnet-tools.json`):

```bash
cd backend
dotnet tool restore
dotnet dotnet-ef migrations add NombreDeLaMigracion --project src/Andje.Chat.Api --output-dir Data/Migrations
```

- La CLI usa `DesignTimeDbContextFactory` (localhost:5433); no necesita la
  base arriba para generar migraciones, solo para aplicarlas.

## Configuración de conexión

| Contexto | Fuente |
| --- | --- |
| Docker Compose (api) | Variable `ConnectionStrings__ChatDb` (host `db:5432`) |
| `dotnet run` local | `appsettings.json` → `localhost:5433` |
| Pruebas de integración | `localhost:${ANDJE_DB_PORT:-5433}`, base `andje_chat_test` |

El puerto host por defecto es **5433** (no 5432) para no chocar con
instalaciones locales de PostgreSQL; se cambia con `ANDJE_DB_PORT` (ver
`.env.example`). La contraseña por defecto `andje_dev_local` es solo de
desarrollo local.

## Estrategia de pruebas

- `RealtimeFlowTests` / `SmokeTests`: hub completo con
  `InMemoryConversationStore` (rápidas, sin Docker).
- `PostgresConversationStoreTests`: contra PostgreSQL real
  (`docker compose up -d db`). Recrean la base `andje_chat_test` desde cero y
  aplican la migración en cada ejecución (valida reproducibilidad). Si la
  base no está disponible se **omiten** con un aviso, no fallan.

## Inspección manual de la base

```bash
docker exec -it andje-chat-db-1 psql -U andje -d andje_chat

-- conversaciones
SELECT "Id", "Status", "VisitorDisplayName", "CreatedAtUtc" FROM "Conversations" ORDER BY "CreatedAtUtc";

-- mensajes de una conversación
SELECT "SenderType", "Body", "CreatedAtUtc" FROM "Messages" WHERE "ConversationId" = '<id>' ORDER BY "CreatedAtUtc";

-- auditoría
SELECT "EventType", "ActorType", "DataJson", "CreatedAtUtc" FROM "AuditEvents" ORDER BY "CreatedAtUtc";
```

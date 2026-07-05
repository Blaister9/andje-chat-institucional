# Persistencia y auditoria

Fase 03 agrega cierre persistido de conversaciones y auditoria
`conversation.closed` sobre la base de fase 02.

## Arquitectura

- EF Core 8 + Npgsql sobre PostgreSQL 16.
- `ChatDbContext` modela `Conversations`, `Messages` y `AuditEvents`.
- `ChatHub` depende de `IConversationStore`.
- `PostgresConversationStore` es la implementacion normal.
- `InMemoryConversationStore` se mantiene para pruebas rapidas del hub.
- Cada escritura guarda cambios de dominio y auditoria en un unico
  `SaveChangesAsync`.

## Esquema

La migracion inicial ya contiene los campos necesarios para fase 03:

| Tabla | Campos relevantes |
| --- | --- |
| `Conversations` | `Status`, `CreatedAtUtc`, `UpdatedAtUtc`, `ClosedAtUtc` |
| `Messages` | `ConversationId`, `SenderType`, `Body`, `CreatedAtUtc` |
| `AuditEvents` | `ConversationId`, `EventType`, `ActorType`, `DataJson`, `CreatedAtUtc` |

No se requiere migracion nueva para fase 03 porque `Status` se persiste como
texto y `ClosedAtUtc` ya existia como nullable.

## Eventos de auditoria

| EventType | ActorType | Disparador | DataJson |
| --- | --- | --- | --- |
| `conversation.started` | `Visitor` | El visitante inicia una conversacion | `null` |
| `message.sent.visitor` | `Visitor` | El visitante envia un mensaje | `{ "messageId": "..." }` |
| `message.sent.agent` | `Agent` | El agente envia un mensaje | `{ "messageId": "..." }` |
| `conversation.activated` | `Agent` | Primera respuesta del agente cambia `Pending` a `Active` | `{ "messageId": "..." }` |
| `conversation.closed` | `Agent` | La consola cierra una conversacion | `null` |

Reglas de privacidad:

- `DataJson` contiene referencias tecnicas, no cuerpo de mensajes.
- Los logs de API no registran texto de mensajes.
- Los timestamps se guardan en UTC.

## Consultas manuales

```sql
SELECT "Id", "VisitorDisplayName", "Status", "CreatedAtUtc", "UpdatedAtUtc", "ClosedAtUtc"
FROM "Conversations"
ORDER BY "CreatedAtUtc" DESC
LIMIT 5;

SELECT "ConversationId", "SenderType", LEFT("Body", 40) AS "BodyPreview", "CreatedAtUtc"
FROM "Messages"
ORDER BY "CreatedAtUtc" DESC
LIMIT 10;

SELECT "ConversationId", "EventType", "ActorType", "DataJson", "CreatedAtUtc"
FROM "AuditEvents"
ORDER BY "CreatedAtUtc" DESC
LIMIT 10;
```

La evidencia esperada para fase 03 es una conversacion con `Status = Closed`,
`ClosedAtUtc` no nulo y un evento `conversation.closed`.

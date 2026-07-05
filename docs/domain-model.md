# Modelo de dominio — Chat institucional ANDJE

> Actualizado en la fase 02: `Conversation`, `Message` y `AuditEvent` ya están
> implementadas y persistidas en PostgreSQL (ver
> [persistence-audit.md](persistence-audit.md)). `Visitor` y `Agent` como
> tablas propias llegan con la fase de autenticación; por ahora el visitante
> es anónimo y su nombre opcional vive en `Conversation.VisitorDisplayName`.

## Visión general

```
Visitor 1──────* Conversation *──────1 Agent (opcional hasta asignación)
                    │ 1
                    │
                    * Message
                    │
                    * AuditEvent (también referencia a Agent/system)
```

## Entidades

### Visitor (ciudadano)

Persona que abre el chat desde el widget. Anónima en el MVP: se identifica
por un identificador de sesión, con datos personales mínimos y opcionales.

| Campo         | Tipo        | Notas                                          |
| ------------- | ----------- | ---------------------------------------------- |
| Id            | UUID        |                                                |
| DisplayName   | string?     | Nombre que el ciudadano decide dar (opcional)  |
| ContactEmail  | string?     | Opcional, solo si pide seguimiento             |
| CreatedAt     | timestamptz |                                                |

*Minimización de datos:* no se piden cédula, teléfono ni datos sensibles.
Ver [privacy-security-baseline.md](privacy-security-baseline.md).

### Agent (agente interno)

Funcionario o contratista que atiende conversaciones desde la consola.
En fases futuras se vincula al directorio institucional (SSO).

| Campo       | Tipo        | Notas                                    |
| ----------- | ----------- | ---------------------------------------- |
| Id          | UUID        |                                          |
| FullName    | string      |                                          |
| Email       | string      | Correo institucional, único              |
| Role        | enum        | `Agent`, `Supervisor`, `Admin`           |
| IsActive    | bool        | Baja lógica; nunca se borra (auditoría)  |
| CreatedAt   | timestamptz |                                          |

### Conversation (conversación) — implementada (fase 02)

Hilo entre un visitante y (a lo sumo) un agente asignado.

| Campo              | Tipo        | Notas                                       |
| ------------------ | ----------- | ------------------------------------------- |
| Id                 | UUID        |                                             |
| VisitorDisplayName | string?     | Máx. 80; sustituye a VisitorId hasta la fase de autenticación |
| Status             | enum (texto)| `Pending` \| `Active` (`Closed` en fase futura) |
| CreatedAtUtc       | timestamptz |                                             |
| UpdatedAtUtc       | timestamptz | Última escritura sobre la conversación      |
| ClosedAtUtc        | timestamptz?| Columna ya creada; el cierre aún no se implementa |

Campos previstos para fases futuras: `AgentId` (FK, con autenticación) y
`Channel` (cuando exista más de un canal).

**Ciclo de vida (`Status`):**

```
Pending ──(agente toma la conversación)──▶ Active ──(cierre)──▶ Closed
   │                                                              ▲
   └──(visitante abandona / timeout)──────────────────────────────┘
```

### Message (mensaje) — implementada (fase 02, tabla `Messages`)

| Campo          | Tipo        | Notas                                       |
| -------------- | ----------- | ------------------------------------------- |
| Id             | UUID        |                                             |
| ConversationId | UUID (FK)   | Borrado en cascada con la conversación      |
| SenderType     | enum (texto)| `Visitor`, `Agent` (`System` en fase futura)|
| Body           | varchar(2000)| Texto plano; mismo límite que valida el hub |
| CreatedAtUtc   | timestamptz | Inmutable: los mensajes no se editan        |

Campos previstos para fases futuras: `SenderId` (con autenticación) y
`Metadata` jsonb (adjuntos, IA asistida). Hacia los clientes el DTO conserva
los nombres `Content`/`SentAt` de la fase 01.

### AuditEvent (evento de auditoría) — implementada (fase 02)

Registro inmutable (solo inserción) de todo hecho relevante.

| Campo          | Tipo        | Notas                                        |
| -------------- | ----------- | -------------------------------------------- |
| Id             | UUID        |                                              |
| ConversationId | UUID?       | Nulo para eventos globales (p. ej. login futuro) |
| ActorType      | string      | `Visitor`, `Agent`, `System`                 |
| EventType      | varchar(100)| Catálogo actual en [persistence-audit.md](persistence-audit.md) |
| DataJson       | jsonb?      | Solo referencias (ids); nunca contenido de mensajes |
| CreatedAtUtc   | timestamptz |                                              |

Campo previsto para fases futuras: `ActorId` (con autenticación).

## Reglas de negocio iniciales

1. Una conversación `Pending` no tiene agente; pasa a `Active` solo cuando un
   agente la toma (queda `AuditEvent: agent.assigned`).
2. Mensajes y eventos de auditoría son inmutables: nunca UPDATE ni DELETE
   desde la aplicación.
3. El cierre de conversación siempre registra quién la cerró (agente,
   visitante o sistema por inactividad).
4. La retención y anonimización de datos personales sigue la política de la
   línea base de privacidad (pendiente de definir plazos con el área
   jurídica).

## Fuera del modelo (fases futuras)

- Departamentos/colas múltiples y horarios de atención.
- Adjuntos de archivos.
- Sugerencias de IA (el campo `Metadata` de `Message` es el punto de
  extensión previsto).
- Encuestas de satisfacción post-conversación.

# Modelo de dominio — Chat institucional ANDJE

> Fase 00: este modelo es la referencia conceptual para la fase de
> persistencia (EF Core + migraciones). Aún no hay entidades implementadas
> en código.

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

### Conversation (conversación)

Hilo entre un visitante y (a lo sumo) un agente asignado.

| Campo        | Tipo        | Notas                                        |
| ------------ | ----------- | -------------------------------------------- |
| Id           | UUID        |                                              |
| VisitorId    | UUID (FK)   |                                              |
| AgentId      | UUID? (FK)  | Nulo mientras espera en cola                 |
| Status       | enum        | Ver ciclo de vida                            |
| Channel      | string      | `widget` (único canal del MVP)               |
| StartedAt    | timestamptz |                                              |
| ClosedAt     | timestamptz?|                                              |

**Ciclo de vida (`Status`):**

```
Pending ──(agente toma la conversación)──▶ Active ──(cierre)──▶ Closed
   │                                                              ▲
   └──(visitante abandona / timeout)──────────────────────────────┘
```

### Message (mensaje)

| Campo          | Tipo        | Notas                                       |
| -------------- | ----------- | ------------------------------------------- |
| Id             | UUID        |                                             |
| ConversationId | UUID (FK)   |                                             |
| SenderType     | enum        | `Visitor`, `Agent`, `System`                |
| SenderId       | UUID?       | Nulo para `System`                          |
| Content        | text        | Texto plano en el MVP                       |
| Metadata       | jsonb       | Extensible (adjuntos, IA futura)            |
| SentAt         | timestamptz | Inmutable: los mensajes no se editan        |

### AuditEvent (evento de auditoría)

Registro inmutable (solo inserción) de todo hecho relevante.

| Campo          | Tipo        | Notas                                        |
| -------------- | ----------- | -------------------------------------------- |
| Id             | UUID        |                                              |
| ConversationId | UUID? (FK)  | Nulo para eventos globales (p. ej. login)    |
| ActorType      | enum        | `Visitor`, `Agent`, `System`                 |
| ActorId        | UUID?       |                                              |
| EventType      | string      | `conversation.started`, `agent.assigned`, `conversation.closed`, … |
| Data           | jsonb       | Detalle del evento                           |
| OccurredAt     | timestamptz |                                              |

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

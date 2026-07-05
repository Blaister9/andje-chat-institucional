# Modelo de dominio - Chat institucional ANDJE

> Actualizado en fase 03: el ciclo minimo operativo de conversacion es
> `Pending -> Active -> Closed`. `Conversation`, `Message` y `AuditEvent`
> estan implementadas y persistidas en PostgreSQL.

## Entidades implementadas

### Conversation

Hilo entre un visitante y la consola de agentes. En esta fase no existe
asignacion explicita de agente ni autenticacion real.

| Campo | Tipo | Notas |
| --- | --- | --- |
| `Id` | uuid | Identificador de la conversacion |
| `VisitorDisplayName` | varchar(80) null | Nombre opcional indicado por el visitante |
| `Status` | varchar(20) | `Pending`, `Active` o `Closed` |
| `CreatedAtUtc` | timestamptz | Creacion en UTC |
| `UpdatedAtUtc` | timestamptz | Ultima modificacion en UTC |
| `ClosedAtUtc` | timestamptz null | Momento UTC del cierre por consola |

### Ciclo de vida

```text
Pending -> Active -> Closed
```

- `Pending`: conversacion iniciada por el visitante; aun no hay respuesta de agente.
- `Active`: ya hubo al menos una respuesta del agente.
- `Closed`: el agente cerro la conversacion desde consola; no se permiten nuevos mensajes.

Reglas:

- La primera respuesta del agente cambia `Pending` a `Active`.
- `CloseConversation` cambia `Pending` o `Active` a `Closed`, setea `ClosedAtUtc`
  y actualiza `UpdatedAtUtc`.
- Cerrar una conversacion ya cerrada es idempotente y no duplica auditoria.
- El historial sigue consultable despues del cierre.

### Message

| Campo | Tipo | Notas |
| --- | --- | --- |
| `Id` | uuid | Identificador del mensaje |
| `ConversationId` | uuid FK | Conversacion propietaria |
| `SenderType` | varchar(20) | `Visitor` o `Agent` |
| `Body` | varchar(2000) | Texto plano del mensaje |
| `CreatedAtUtc` | timestamptz | Insercion en UTC |

Los mensajes son inmutables. No se aceptan mensajes vacios ni mensajes nuevos
cuando la conversacion esta `Closed`.

### AuditEvent

| Campo | Tipo | Notas |
| --- | --- | --- |
| `Id` | uuid | Identificador del evento |
| `ConversationId` | uuid null | Conversacion asociada |
| `EventType` | varchar(100) | Catalogo en `docs/persistence-audit.md` |
| `ActorType` | varchar(20) | `Visitor`, `Agent` o `System` futuro |
| `DataJson` | jsonb null | Referencias tecnicas, nunca cuerpo de mensajes |
| `CreatedAtUtc` | timestamptz | Insercion en UTC |

## Datos personales y limites

El visitante sigue siendo anonimo para el sistema. El unico dato opcional que
se solicita en esta fase es el nombre de visualizacion. No se piden documento,
telefono, direccion, correo, expediente ni otros datos sensibles.

## Fuera de esta fase

- Autenticacion real y directorio institucional.
- Asignacion explicita de agente.
- Departamentos, transferencia, metricas o dashboard administrativo.
- Adjuntos, IA, retencion automatica y anonimizacion automatica.

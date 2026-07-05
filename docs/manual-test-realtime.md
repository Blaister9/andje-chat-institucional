# Prueba manual - realtime y ciclo de vida

Verifica el flujo fase 03: inicio, mensajes, reanudacion tras recarga, cierre,
bloqueo de mensajes y evidencia en PostgreSQL.

## Preparacion

```powershell
docker compose up --build -d
curl.exe -s http://localhost:8080/health
```

URLs:

- Consola: `http://localhost:5173`
- Widget demo: `http://localhost:5174`
- API health: `http://localhost:8080/health`

## Flujo manual

1. Abrir widget demo.
2. Abrir el panel del widget.
3. Iniciar conversacion con un nombre de prueba no sensible, por ejemplo `Laura`.
4. Enviar desde widget: `Hola, necesito orientacion general`.
5. Abrir consola.
6. Confirmar que la conversacion aparece en cola sin refrescar.
7. Seleccionar la conversacion.
8. Responder desde consola: `Hola, con gusto te orientamos.`
9. Confirmar que el widget recibe la respuesta sin refrescar.
10. Recargar la pagina del widget.
11. Confirmar que el widget recupera la misma conversacion y el historial.
12. Cerrar la conversacion desde consola con `Cerrar conversacion`.
13. Confirmar que el widget muestra el mensaje institucional de cierre sin refrescar.
14. Confirmar que el input del widget queda bloqueado.
15. Confirmar que la cola de consola oculta la conversacion cerrada por defecto.
16. Activar `Ver cerradas` y confirmar que la conversacion cerrada aparece.
17. Usar `Nueva conversacion` en el widget y confirmar que vuelve al formulario inicial.
18. Revisar DevTools en consola y widget: no deben existir errores salvo desconexiones
    esperadas durante reinicios deliberados.

## Evidencia SQL

```powershell
@'
SELECT "Id", "VisitorDisplayName", "Status", "CreatedAtUtc", "UpdatedAtUtc", "ClosedAtUtc"
FROM "Conversations"
ORDER BY "CreatedAtUtc" DESC
LIMIT 5;
'@ | docker exec -i andje-chat-db-1 psql -U andje -d andje_chat

@'
SELECT "ConversationId", "SenderType", LEFT("Body", 40) AS "BodyPreview", "CreatedAtUtc"
FROM "Messages"
ORDER BY "CreatedAtUtc" DESC
LIMIT 10;
'@ | docker exec -i andje-chat-db-1 psql -U andje -d andje_chat

@'
SELECT "ConversationId", "EventType", "ActorType", "DataJson", "CreatedAtUtc"
FROM "AuditEvents"
ORDER BY "CreatedAtUtc" DESC
LIMIT 10;
'@ | docker exec -i andje-chat-db-1 psql -U andje -d andje_chat
```

Resultado esperado:

- `Conversations.Status = Closed`.
- `Conversations.ClosedAtUtc` no nulo.
- Los mensajes previos al cierre siguen persistidos.
- `AuditEvents` contiene `conversation.closed`.
- `AuditEvents.DataJson` no contiene cuerpo de mensajes.

## Verificacion de logs

```powershell
docker logs andje-chat-api-1 | Select-String 'Hola, necesito orientacion general'
docker logs andje-chat-api-1 | Select-String 'Hola, con gusto te orientamos'
```

Resultado esperado: cero coincidencias.

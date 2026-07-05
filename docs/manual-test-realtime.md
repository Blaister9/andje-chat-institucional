# Prueba manual - realtime, acceso de consola y ciclo de vida

Verifica el flujo fase 04: consola bloqueada sin acceso, sesion local de
agente, inicio, mensajes, cierre, bloqueo de mensajes y evidencia en
PostgreSQL.

## Preparacion

```powershell
docker compose up --build -d
curl.exe -s http://localhost:8080/health
```

URLs:

- Consola: `http://localhost:5173`
- Widget demo: `http://localhost:5174`
- API health: `http://localhost:8080/health`

Codigo local por defecto en Docker Compose: `andje-agent-local`.

## Flujo manual

1. Abrir consola en navegador limpio.
2. Confirmar que aparece `Acceso consola de agentes`.
3. Confirmar que no se ve cola de conversaciones.
4. Intentar ingresar un codigo incorrecto y confirmar error visible.
5. Ingresar nombre `Agente QA` y codigo local valido.
6. Confirmar que entra a la consola, conecta realtime y muestra `Salir`.
7. Abrir widget demo.
8. Abrir el panel del widget.
9. Iniciar conversacion con un nombre de prueba no sensible, por ejemplo
   `Visitante QA Auth`.
10. Enviar desde widget: `Mensaje QA fase 04`.
11. Confirmar que la conversacion aparece en cola sin refrescar.
12. Seleccionar la conversacion.
13. Responder desde consola: `Respuesta agente fase 04`.
14. Confirmar que el widget recibe la respuesta sin refrescar.
15. Cerrar la conversacion desde consola con `Cerrar conversacion`.
16. Confirmar que el widget muestra el mensaje institucional de cierre sin refrescar.
17. Confirmar que el input del widget queda bloqueado.
18. Confirmar que la cola de consola oculta la conversacion cerrada por defecto.
19. Usar `Salir` y confirmar que vuelve a la pantalla de acceso.
20. Revisar DevTools en consola y widget: no deben existir errores salvo desconexiones
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
- `AuditEvents` contiene `message.sent.agent` y `conversation.closed` con
  `agentSessionId` y `agentDisplayName`.
- `AuditEvents.DataJson` no contiene token, codigo de acceso ni cuerpo de
  mensajes.

## Verificacion de logs

```powershell
docker logs andje-chat-api-1 | Select-String 'Hola, necesito orientacion general'
docker logs andje-chat-api-1 | Select-String 'Hola, con gusto te orientamos'
docker logs andje-chat-api-1 | Select-String 'andje-agent-local'
docker logs andje-chat-api-1 | Select-String 'Mensaje QA fase 04'
docker logs andje-chat-api-1 | Select-String 'Respuesta agente fase 04'
```

Resultado esperado: cero coincidencias.

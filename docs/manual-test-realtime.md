# Prueba manual — flujo de chat en tiempo real (fase 01)

Verifica el flujo completo visitante ↔ agente sin persistencia. Duración
aproximada: 5 minutos.

## Requisitos

- Docker en ejecución.
- Puertos libres: 8080 (API), 5173 (consola), 5174 (widget), 5433 (Postgres).

## Preparación

```bash
docker compose up --build -d
```

Esperar a que los cuatro contenedores estén arriba (la consola y el widget
tardan ~30 s adicionales por el `npm install` interno). Verificar:

1. `http://localhost:8080/health` responde `Healthy`.
2. `http://localhost:5173` muestra la consola con el estado **conectado**
   (esquina superior derecha) y el mensaje "Sin conversaciones".
3. `http://localhost:5174` muestra la página demo con el botón flotante 💬.

## Prueba paso a paso

Abrir dos ventanas del navegador lado a lado: **A** = consola (5173),
**B** = widget demo (5174). No refrescar ninguna durante la prueba.

| # | Acción | Resultado esperado |
|---|--------|--------------------|
| 1 | En **B**, clic en el botón 💬 | Se abre el panel con el campo "Nombre (opcional)" |
| 2 | En **B**, escribir un nombre (p. ej. `María`) y clic en "Iniciar chat" | El panel cambia a la vista de chat con campo de mensaje |
| 3 | Mirar **A** (sin refrescar) | Aparece "María" en la cola con badge **En espera** |
| 4 | En **B**, escribir `Hola, tengo una consulta` y Enviar | El mensaje aparece como burbuja azul a la derecha |
| 5 | Mirar **A** (sin refrescar) | El ítem "María" muestra badge rojo de no leído (1) |
| 6 | En **A**, clic sobre "María" | Se abre la conversación y se ve `Hola, tengo una consulta` |
| 7 | En **A**, escribir `Buen día, con gusto le ayudo` y Responder | La respuesta aparece como burbuja azul a la derecha |
| 8 | Mirar **B** (sin refrescar) | La respuesta del agente aparece como burbuja gris a la izquierda; en **A** el badge cambia a **Activa** |
| 9 | Repetir 4 y 7 varias veces | Los mensajes fluyen en ambos sentidos sin refrescar |
| 10 | En ambas ventanas, abrir DevTools → Console | Sin errores |

## Prueba de reinicio de la API (persistencia, fase 02)

1. Con la conversación abierta, ejecutar `docker compose restart api`.
2. El widget muestra "Reconectando…" y la consola pasa a **desconectado**.
3. Al volver la API (~10 s), ambos se reconectan solos: la consola recarga la
   cola con la conversación persistida y el widget repinta el historial
   completo desde PostgreSQL. Se puede seguir conversando donde iba.

> Durante el corte, la consola del navegador registra errores transitorios
> del cliente SignalR (WebSocket cerrado / negociación fallida). Es el
> comportamiento esperado de una desconexión real; desaparecen al reconectar
> y el flujo normal (pasos 1–10) no genera ningún error.

## Verificación en base de datos (fase 02)

```bash
docker exec -it andje-chat-db-1 psql -U andje -d andje_chat \
  -c 'SELECT "Status", "VisitorDisplayName" FROM "Conversations";' \
  -c 'SELECT "SenderType", left("Body", 30) FROM "Messages" ORDER BY "CreatedAtUtc";' \
  -c 'SELECT "EventType", "ActorType" FROM "AuditEvents" ORDER BY "CreatedAtUtc";'
```

Deben aparecer la conversación (`Active`), los mensajes de ambos lados y los
eventos `conversation.started`, `message.sent.visitor`, `message.sent.agent`
y `conversation.activated`.

## Limpieza

```bash
docker compose down
```

## Resultado de la última ejecución

Ejecutada el 2026-07-05 sobre `feat/02-persistence-audit-foundation`: pasos
1–10, reinicio de la API y verificación en base de datos pasaron (verificado
con dos páginas controladas por navegador, sin errores en la consola de
DevTools).

# Agent console product experience

Microfase 11 convierte la consola de agentes en una experiencia de producto
demo: tablero operativo, cola filtrable, detalle de conversacion, respuestas
rapidas configurables, notas internas y etiquetas visuales.

No es una consola productiva institucional. El acceso sigue siendo local/dev
con token opaco temporal; no hay Entra ID, OIDC, roles reales ni trazabilidad
institucional completa.

## Que incluye

- Layout de pantalla completa con navegacion lateral.
- Header con identidad del funcionario y accion de salida.
- Tarjetas de resumen operativo: abiertas, en espera, activas, cerradas,
  mensajes y estado realtime.
- Cola de conversaciones ordenada por actividad reciente.
- Filtros: abiertas, en espera, activas, cerradas y todas.
- Busqueda por nombre del ciudadano, preview y mensajes ya cargados.
- Detalle con timeline, estado, fecha, cierre y composer bloqueado si la
  conversacion esta cerrada.
- Respuestas rapidas insertables en el composer.
- Seccion de configuracion para crear, editar y desactivar respuestas rapidas.
- Catalogo persistido de etiquetas demo.
- Asignacion y retiro de etiquetas por conversacion.
- Notas internas por conversacion.

## Modelo de datos

La fase agrega tablas EF Core:

- `CannedResponses`: respuestas rapidas configurables.
- `ConversationTags`: catalogo de etiquetas demo.
- `ConversationTagAssignments`: relacion conversacion-etiqueta.
- `InternalNotes`: notas internas por conversacion.

La migracion `ConsoleProductExperience` crea las tablas y carga datos semilla:

- Respuestas rapidas: saludo institucional, solicitud de datos generales,
  cierre amable.
- Etiquetas: `Orientacion`, `Urgente`, `Tramite`, `Seguimiento`, `PQRS`.

## Seguridad y privacidad

- Todos los endpoints `/api/console/*` requieren `Authorization: Bearer <token>`
  emitido por `/api/agent-sessions`.
- El widget no consume endpoints de consola.
- Las notas internas no se envian por SignalR al ciudadano.
- Las etiquetas y respuestas rapidas no se exponen al widget.
- La auditoria registra ids, tipo de evento, titulo de respuesta y nombre de
  etiqueta cuando aplica.
- La auditoria no guarda cuerpos de mensajes, notas internas ni respuestas
  rapidas.
- Los logs no deben incluir mensajes, notas, codigos ni tokens.

## Endpoints de consola

```text
GET    /api/console/summary
GET    /api/console/conversations
GET    /api/console/canned-responses
POST   /api/console/canned-responses
PUT    /api/console/canned-responses/{id}
PATCH  /api/console/canned-responses/{id}/deactivate
GET    /api/console/tags
POST   /api/console/conversations/{conversationId}/tags/{tagId}
DELETE /api/console/conversations/{conversationId}/tags/{tagId}
GET    /api/console/conversations/{conversationId}/notes
POST   /api/console/conversations/{conversationId}/notes
```

## Demo sugerida

1. Levantar demo:

   ```powershell
   .\scripts\demo\start-demo.ps1
   .\scripts\demo\check-demo.ps1
   ```

2. Abrir consola `http://localhost:5173`.
3. Ingresar como `Funcionario Demo` con codigo `andje-agent-local`.
4. Mostrar dashboard y cola vacia o datos existentes.
5. Ir a `Configuracion`.
6. Crear respuesta rapida:
   - Titulo: `Saludo demo`
   - Cuerpo: `Hola, con gusto te orientamos.`
7. Volver a `Atencion`.
8. Abrir widget `http://localhost:5174`.
9. Iniciar chat como `Ciudadano Demo UX`.
10. Enviar `Consulta demo consola`.
11. Seleccionar la conversacion en cola.
12. Insertar la respuesta rapida y responder.
13. Agregar nota interna `Nota interna de seguimiento`.
14. Asignar etiqueta `Seguimiento`.
15. Cerrar la conversacion.
16. Confirmar que el widget recibe la respuesta y el cierre, pero no la nota.

## Limitaciones

- No hay roles reales ni administracion avanzada.
- No hay asignacion de conversaciones entre funcionarios.
- No hay SLA, departamentos, adjuntos, exportacion ni notificaciones externas.
- La busqueda usa datos disponibles en la consola; no es busqueda indexada.
- Las etiquetas se crean por migracion y se administran como catalogo demo.
- La consola no reemplaza controles institucionales de autenticacion,
  autorizacion, retencion ni monitoreo.

## Verificacion local

```powershell
docker compose up -d db
dotnet test backend/Andje.Chat.sln

cd apps\console
npm ci
npm run build
npm audit --audit-level=high
cd ..\..
```

Para comprobar que no se filtren cuerpos a logs:

```powershell
docker logs andje-chat-api-1 | Select-String "Consulta demo consola"
docker logs andje-chat-api-1 | Select-String "Hola, con gusto te orientamos"
docker logs andje-chat-api-1 | Select-String "Nota interna de seguimiento"
docker logs andje-chat-api-1 | Select-String "andje-agent-local"
```

El resultado esperado es cero coincidencias.

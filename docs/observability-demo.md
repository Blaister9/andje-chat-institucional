# Observability demo

Observabilidad basica para demo tecnica/local. No es monitoreo productivo,
no incluye alertas, dashboard, trazas distribuidas ni metricas institucionales.

## Endpoints

### `/health`

Respuesta simple compatible:

```text
Healthy
```

Debe incluir headers de seguridad y `X-Request-ID`.

### `/api/diagnostics/status`

Disponible en:

- `Development`
- `Test`
- otros entornos solo si `Diagnostics:Enabled=true`

Muestra:

- estado general;
- entorno;
- estado de base de datos;
- hora UTC;
- version `dev`;
- conteos operativos si `Diagnostics:IncludeCounts=true`.

No muestra:

- connection strings;
- passwords;
- tokens;
- codigos de acceso;
- cuerpos de mensajes;
- headers completos;
- variables de entorno completas.

Ejemplo:

```json
{
  "status": "Healthy",
  "environment": "Development",
  "database": "Reachable",
  "utcNow": "2026-07-06T00:00:00Z",
  "version": "dev",
  "counts": {
    "conversationsTotal": 0,
    "conversationsOpen": 0,
    "conversationsClosed": 0,
    "messagesTotal": 0,
    "auditEventsTotal": 0
  }
}
```

## Scripts

```powershell
.\scripts\demo\check-demo.ps1
.\scripts\demo\show-demo-status.ps1
```

`check-demo.ps1` valida health, headers, `X-Request-ID`, diagnostico si esta
disponible, consola, widget y PostgreSQL.

`show-demo-status.ps1` muestra estado y conteos. No imprime mensajes, tokens,
codigos ni cadenas de conexion.

## Logs y Request ID

Cada respuesta incluye `X-Request-ID`. Si ocurre un error:

1. Copie el valor de `X-Request-ID`.
2. Revise logs:

```powershell
docker compose logs api
```

El middleware usa el mismo valor como scope de logging `RequestId`. No se
registran cuerpos de mensajes, tokens ni codigos de acceso.

## Consultas operativas en PostgreSQL

Conteos:

```powershell
$sql = 'SELECT COUNT(*) AS conversations FROM "Conversations"; SELECT COUNT(*) AS messages FROM "Messages"; SELECT COUNT(*) AS audit_events FROM "AuditEvents";'
$sql | docker compose exec -T db psql -U andje -d andje_chat -v ON_ERROR_STOP=1
```

Auditoria sin contenido de mensajes:

```powershell
$sql = 'SELECT "EventType", "ActorType", "ConversationId", "CreatedAtUtc" FROM "AuditEvents" ORDER BY "CreatedAtUtc" DESC LIMIT 20;'
$sql | docker compose exec -T db psql -U andje -d andje_chat -v ON_ERROR_STOP=1
```

## Limpiar datos demo

```powershell
.\scripts\demo\reset-demo-data.ps1
```

El reset borra conversaciones, mensajes y auditoria sin borrar estructura ni
volumenes.

## Troubleshooting

- API no levanta: `docker compose logs api`, revisar `/health`.
- DB no responde: `docker compose ps db` y `docker compose logs db`.
- Consola no conecta: validar `DEMO_API_URL`, CORS y API health.
- Widget no recibe mensajes: revisar SignalR en logs de navegador y API.
- CORS falla: agregar origen exacto en `DEMO_ALLOWED_ORIGINS`, nunca `*`.
- Rate limit bloquea login: esperar la ventana configurada o reiniciar demo.
- Token local expira: salir e ingresar de nuevo con codigo demo.
- Puerto ocupado: detener proceso local o ajustar puerto antes de iniciar.

## Pendientes

- Metricas institucionales.
- OpenTelemetry.
- Dashboard operacional.
- Alertas.
- Politicas formales de retencion/anonimizacion.

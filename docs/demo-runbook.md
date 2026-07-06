# Demo runbook

Perfil reproducible para mostrar el prototipo en una PC local o en una red LAN
controlada. No es despliegue productivo, no tiene TLS real y no reemplaza
autenticacion institucional.

## Requisitos

- Git.
- Docker Desktop iniciado.
- PowerShell.
- Puertos disponibles: `8080`, `5173`, `5174`, `5433`.
- Red local controlada si se hace demo LAN.

## Preparar una PC nueva

```powershell
git clone https://github.com/Blaister9/andje-chat-institucional.git
cd andje-chat-institucional
git checkout develop
git pull --ff-only origin develop
```

No cree `.env` ni `.env.demo` con secretos reales. Si necesita ajustar valores
de demo, copie manualmente `.env.demo.example` a `.env.demo` en su equipo local
y no lo versione.

## Levantar demo

```powershell
.\scripts\demo\start-demo.ps1
.\scripts\demo\check-demo.ps1
.\scripts\demo\show-demo-status.ps1
```

URLs locales:

| Uso | URL |
| --- | --- |
| Widget demo | `http://localhost:5174` |
| Consola | `http://localhost:5173` |
| API health | `http://localhost:8080/health` |
| Diagnostico demo | `http://localhost:8080/api/diagnostics/status` |
| PostgreSQL | `localhost:5433`, db `andje_chat`, usuario `andje` |

Credenciales demo de consola:

- Nombre sugerido: `Funcionario Demo`.
- Codigo local: `andje-agent-local`.

Estos valores son solo demo/dev. No son autenticacion institucional.

## Guion paso a paso

1. Abrir `http://localhost:5174`.
2. Iniciar chat como `Ciudadano Demo`.
3. Enviar `Consulta demo institucional`.
4. Abrir `http://localhost:5173`.
5. Iniciar sesion como `Funcionario Demo` con codigo `andje-agent-local`.
6. Seleccionar la conversacion en cola.
7. Responder `Respuesta demo del funcionario`.
8. Confirmar que el widget recibe la respuesta.
9. Cerrar la conversacion desde consola.
10. Confirmar que el widget queda cerrado y ofrece nueva conversacion.
11. Mostrar auditoria tecnica con consulta SQL.
12. Mostrar estado operativo con `.\scripts\demo\show-demo-status.ps1`.

## Consultar auditoria

```powershell
$sql = 'SELECT "EventType", "ActorType", "ConversationId", "CreatedAtUtc" FROM "AuditEvents" ORDER BY "CreatedAtUtc" DESC LIMIT 20;'
$sql | docker compose exec -T db psql -U andje -d andje_chat -v ON_ERROR_STOP=1
```

La auditoria no guarda contenido de mensajes. Debe usarse para mostrar eventos
tecnicos, no datos reales.

## Revisar estado operativo

```powershell
.\scripts\demo\show-demo-status.ps1
```

El diagnostico muestra estado, base de datos y conteos. No muestra cuerpos de
mensajes, tokens, codigos ni cadenas de conexion. Si ocurre un error, copie el
header `X-Request-ID` de la respuesta y busquelo en logs de API.

## Limpiar datos demo

Para borrar conversaciones, mensajes y eventos de auditoria sin borrar
estructura ni volumenes:

```powershell
.\scripts\demo\reset-demo-data.ps1
```

El script exige escribir `RESET DEMO`. Para automatizacion local controlada:

```powershell
.\scripts\demo\reset-demo-data.ps1 -Force
```

## Apagar demo

```powershell
.\scripts\demo\stop-demo.ps1
```

Esto conserva volumenes. Para un reset completo manual, use:

```powershell
docker compose down -v
```

## Demo LAN

1. Identifique la IP local del host, por ejemplo `192.168.1.20`.
2. Verifique firewall para `5173`, `5174` y `8080`.
3. Cree `.env.demo` local, no versionado:

```text
DEMO_API_URL=http://192.168.1.20:8080
DEMO_ALLOWED_ORIGINS=http://192.168.1.20:5173,http://192.168.1.20:5174
```

4. Ejecute `.\scripts\demo\start-demo.ps1`.
5. Desde otra maquina de la red, abra:
   - `http://192.168.1.20:5174`
   - `http://192.168.1.20:5173`

No use `*` en CORS. El backend solo agrega los origenes explicitos definidos en
`DEMO_ALLOWED_ORIGINS`.

Riesgos LAN:

- HTTP sin TLS.
- Codigo local de agente, no autenticacion institucional.
- Red compartida puede observar trafico.
- No usar datos reales.
- No dejar corriendo despues de la demo.

## Que NO decir en demo

- No decir que esta listo para produccion.
- No decir que cumple seguridad institucional.
- No decir que tiene autenticacion Entra ID/OIDC.
- No decir que tiene IA, RAG o bots.
- No decir que tiene HTTPS real o despliegue cloud.
- No ingresar datos personales, expedientes ni informacion sensible real.

## Checklist pre-demo

- `git checkout develop && git pull --ff-only origin develop`.
- `docker compose down`.
- `.\scripts\demo\start-demo.ps1`.
- `.\scripts\demo\check-demo.ps1`.
- Limpiar datos si se requiere: `.\scripts\demo\reset-demo-data.ps1`.
- Probar login de consola.
- Probar widget.
- Confirmar que no se usaran datos reales.
- Confirmar que la red LAN es controlada si aplica.

## Checklist post-demo

- `.\scripts\demo\stop-demo.ps1`.
- Limpiar datos si alguien ingreso informacion sensible accidentalmente.
- Registrar feedback.
- No dejar puertos expuestos en LAN.

## Troubleshooting

- Docker no responde: iniciar Docker Desktop y repetir.
- Puerto ocupado: cerrar proceso local o cambiar el puerto por variable de
  entorno antes de iniciar.
- Health no responde: revisar `docker compose logs api`.
- Diagnostico no responde: confirmar entorno `Development` o
  `Diagnostics:Enabled=true`.
- Consola o widget no abren: revisar `docker compose logs console widget`.
- Login falla: verificar `ANDJE_AGENT_DEV_CODE` o usar `andje-agent-local`.
- LAN no conecta: revisar firewall, IP local y `DEMO_ALLOWED_ORIGINS`.
- CORS bloquea LAN: agregar origen exacto, nunca wildcard.

## Demo detras de proxy HTTPS

Para una demo controlada con TLS terminado por un proxy externo, mantenga el
backend en red interna y configure `.env.demo` local no versionado:

```text
DEMO_API_URL=https://chat-demo.local
DEMO_ALLOWED_ORIGINS=https://chat-demo.local,https://widget-demo.local
ANDJE_FORWARDED_HEADERS_ENABLED=true
ANDJE_FORWARDED_HEADERS_FORWARD_LIMIT=1
ANDJE_FORWARDED_HEADERS_KNOWN_PROXIES=192.168.1.10
ANDJE_FORWARDED_HEADERS_KNOWN_NETWORKS=
ANDJE_HTTPS_REQUIRE=false
ANDJE_HTTPS_HSTS=true
```

El proxy debe enviar `X-Forwarded-Proto=https`; opcionalmente
`X-Forwarded-For` y `X-Forwarded-Host`. No use comodines de CORS ni confie en
forwarded headers de proxies desconocidos.

Detalles: [HTTPS and forwarded headers](https-forwarded-headers.md).

Detalles de operacion demo: [Observability demo](observability-demo.md).

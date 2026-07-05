# Endurecimiento de seguridad y privacidad (fase 07)

Controles tecnicos transversales agregados al backend para reducir riesgos al
demostrar el prototipo. Es una **base tecnica preliminar, alineada con buenas
practicas**; **no** convierte el prototipo en un sistema de produccion y queda
**pendiente de revision juridica y de seguridad institucional**.

## Headers HTTP de seguridad

Middleware global (`Security/SecurityHeadersExtensions.cs`) que agrega a todas
las respuestas:

| Header | Valor | Proposito |
| --- | --- | --- |
| `X-Content-Type-Options` | `nosniff` | Evita sniffing de tipo MIME |
| `X-Frame-Options` | `DENY` | Evita clickjacking por iframe |
| `Referrer-Policy` | `no-referrer` | No filtra URLs por Referer |
| `Permissions-Policy` | `geolocation=(), microphone=(), camera=()` | Desactiva APIs sensibles del navegador |
| `Cache-Control` | `no-store` | El backend solo expone datos/tiempo real |

- **HSTS**: `UseHsts()` se registra solo fuera de desarrollo. En el desarrollo
  local (HTTP) no aplica; el header solo tiene efecto real bajo HTTPS.
- **CSP**: pendiente. El backend no sirve UI y la consola/widget corren con
  Vite dev en esta fase. Una CSP estricta corresponde al despliegue estatico de
  produccion, no a este prototipo.

## CORS explicito por configuracion

- Origenes permitidos en `Cors:AllowedOrigins` (`appsettings.json`). Por defecto
  local: `http://localhost:5173` (consola) y `http://localhost:5174` (widget).
- No se permite `*` porque la politica habilita credenciales
  (`AllowCredentials`); un origen no listado no recibe `Access-Control-Allow-Origin`.
- Para ambientes futuros se agregan/ajustan origenes por configuracion (o su
  equivalente por variable de entorno `Cors__AllowedOrigins__0`, etc.).

## Rate limiting

- Rate limiter nativo de ASP.NET Core 8 (sin dependencias nuevas, sin Redis).
- Politica `agent-session` (ventana fija) aplicada a `POST /api/agent-sessions`
  para frenar fuerza bruta del codigo local/dev.
- Configurable: `RateLimiting:AgentSessionPermitLimit` (por defecto 10) y
  `RateLimiting:AgentSessionWindowSeconds` (por defecto 60). Al exceder se
  responde `429 Too Many Requests`.
- **Limitacion conocida**: la particion usa la IP de la conexion directa.
  Detras de un proxy reverso habria que honrar `X-Forwarded-For`; pendiente para
  el despliegue. El hub SignalR no se limita por request en esta fase; el
  control de abuso ahi se apoya en la validacion de token y en los limites de
  tamano de mensaje.

## Limites de payload

- Mensajes de chat: maximo 2000 caracteres (validado en el hub, sin cambios).
- Nombre de agente/visitante: maximo 80 caracteres.
- Codigo de acceso: se rechaza por encima de 256 caracteres antes de comparar.
- Cuerpo de request HTTP (Kestrel): `Security:MaxRequestBodyBytes`
  (por defecto 1 MiB, muy por debajo del maximo por defecto ~28 MiB).
- Mensaje maximo de SignalR: `Security:SignalRMaxMessageBytes`
  (por defecto 32 KiB), con holgura sobre el limite de 2000 caracteres.

## Validacion de configuracion insegura al arranque

`Security/SecurityStartupValidation.cs` detecta configuraciones peligrosas.
Fuera de desarrollo estos hallazgos son **fatales** (la app no arranca); en
desarrollo se emiten como **advertencias** para no romper el flujo local.

Casos cubiertos:

- `AgentAccess:Enabled=true` con `DevelopmentAccessCode` vacio.
- `Cors:AllowedOrigins` vacio o con `*`.
- `ConnectionStrings:ChatDb` ausente.
- `Database:AutoMigrate=true` fuera de desarrollo.
- `DevelopmentAccessCode` con un valor local/dev conocido fuera de desarrollo.

Los mensajes nunca incluyen valores sensibles (codigo, cadena de conexion):
solo describen el problema y la clave de configuracion.

## Logs sin PII ni secretos

Politica verificada en esta fase:

- No se registran cuerpos de mensajes.
- No se registran codigos de acceso.
- No se registran tokens de sesion.
- No se registra la cadena de conexion.
- La auditoria (`DataJson`) guarda solo referencias (ids), nunca contenido.
- EF Core en nivel `Warning` para no volcar parametros SQL en operacion normal.

## Lo que sigue siendo local/dev y NO esta listo para produccion

- El acceso de consola es un codigo local, **no** autenticacion institucional
  (Entra ID/OIDC pendiente).
- Todo el trafico local es HTTP; falta TLS/HTTPS real y HSTS efectivo.
- El rate limiting no distingue IP real detras de proxy.
- No hay CSP de produccion ni despliegue estatico endurecido.
- No hay retencion ni anonimizacion automatica de datos.

## Checklist antes de una demo

- [ ] No usar datos personales reales ni sensibles.
- [ ] Revisar logs: sin mensajes, codigos ni tokens.
- [ ] Validar CORS: solo los origenes esperados.
- [ ] No exponer el prototipo sin HTTPS.
- [ ] No tratar el codigo local como autenticacion real.
- [ ] No habilitar `Database:AutoMigrate` en produccion.
- [ ] No usar servicios de IA publica con datos de la demo.
- [ ] No subir `.env` ni secretos al repositorio.

## Riesgos antes de exponer en red institucional

- Sin autenticacion institucional real cualquier persona con el codigo local
  puede actuar como agente.
- Sin HTTPS, el token de sesion viaja en claro.
- El almacenamiento de sesiones es en memoria: no sobrevive reinicios ni escala
  horizontalmente.
- Falta gobierno de datos (retencion, anonimizacion, minimizacion formal)
  pendiente de revision juridica y del area de seguridad de la entidad.

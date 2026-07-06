# Fundacion de acceso de consola

Fase 04 agrega una frontera minima entre el widget publico y la consola interna.
Es una base tecnica preliminar para desarrollo local, no autenticacion
institucional final.

## Modelo actual

- El widget publico sigue sin autenticacion de agente.
- La consola muestra `Acceso consola de agentes` si no hay sesion en
  `sessionStorage`.
- La consola solicita nombre sintetico de agente y codigo local de desarrollo.
- `POST /api/agent-sessions` valida el codigo local y emite un token opaco.
- El token se guarda en `sessionStorage` y se usa con `accessTokenFactory` en
  SignalR.
- Las sesiones viven en memoria del backend y expiran por configuracion.
- Reiniciar la API invalida todas las sesiones.

## Configuracion

Variables soportadas:

```text
ANDJE_AGENT_ACCESS_ENABLED=true
ANDJE_AGENT_DEV_CODE=change-me-local-only
ANDJE_AGENT_SESSION_MINUTES=120
```

Docker Compose define `andje-agent-local` como valor local-only para demo. No
debe usarse como secreto real ni como mecanismo de produccion.

## Metodos protegidos

Requieren token de agente valido en cada invocacion:

- `JoinAgentConsole`
- `SendAgentMessage`
- `CloseConversation`

Sin token el hub rechaza con `Agent session is required.`. Con token invalido o
expirado rechaza con `Agent session is invalid or expired.`.

## Metodos publicos

Siguen abiertos para el widget/visitante:

- `StartConversation`
- `JoinConversation`
- `GetConversation`
- `GetConversationHistory`
- `SendVisitorMessage`

Limitacion conocida: el acceso por `conversationId` se acepta para MVP local
porque el identificador es no adivinable, pero debe revisarse antes de
produccion.

## Auditoria

Los eventos de agente incluyen identidad minima de desarrollo:

```json
{
  "messageId": "...",
  "agentSessionId": "...",
  "agentDisplayName": "Agente QA"
}
```

`conversation.closed` incluye `agentSessionId` y `agentDisplayName`. La
auditoria no debe contener token, codigo de acceso ni cuerpo completo de
mensajes.

## Limitaciones

- No es Microsoft Entra ID.
- No es OIDC/OAuth institucional.
- No hay roles, usuarios reales, MFA ni administracion de agentes.
- En navegador, SignalR puede transportar el token en `access_token` durante la
  conexion; esto debe revisarse con la infraestructura futura para evitar logs
  de URL completas.
- La identidad auditada es identidad de desarrollo, no identidad institucional
  verificada.

## Endurecimiento adicional (fase 07)

`POST /api/agent-sessions` recibio controles transversales de seguridad:

- Rate limiting nativo (por defecto 10 peticiones/minuto por IP) que devuelve
  `429` ante fuerza bruta del codigo local.
- Rechazo de codigos de acceso mayores a 256 caracteres antes de compararlos.
- Headers HTTP de seguridad y CORS explicito por configuracion.
- Validacion de configuracion insegura al arranque (por ejemplo acceso
  habilitado sin codigo, `*` en CORS, `AutoMigrate` fuera de desarrollo).

Detalle en [security-privacy-hardening.md](security-privacy-hardening.md).

## Futuro

La siguiente evolucion natural es reemplazar este mecanismo local por Microsoft
Entra ID u OIDC institucional, conservando la separacion entre metodos publicos
de visitante y metodos internos de agente.

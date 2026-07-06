# Experiencia ciudadana del widget (fase 12)

Convierte el widget de demostracion tecnica en una experiencia de atencion
ciudadana presentable. Es una base demostrativa; **no** es un producto
listo para produccion y **no** reemplaza autenticacion institucional, TLS
real ni revision juridica.

## Que cambia para el ciudadano

- **Pagina demo institucional** (`apps/widget/index.html`): encabezado de
  entidad, bloque hero, explicacion del canal, tarjetas de "que puedes hacer"
  y aviso de demostracion. Sin logos oficiales no autorizados.
- **Widget rediseniado** (`apps/widget/src/andje-chat-widget.ts`):
  - Encabezado institucional y estado de conexion (conectado / conectando /
    reconectando / sin conexion).
  - Pantalla de inicio con proposito claro, formulario minimo y consentimiento.
  - Timeline con burbujas diferenciadas (ciudadano a la derecha, funcionario a
    la izquierda) y etiqueta de remitente.
  - Estados: "esperando a un funcionario", "un funcionario esta atendiendo",
    "conversacion cerrada" y estados de error/reconexion.
  - Cierre institucional con encuesta de satisfaccion y transcripcion local.

## Formulario minimo y datos

Se solicita solo:

- Nombre o alias (opcional).
- Tema/categoria (catalogo fijo del widget).
- Aceptacion del aviso de tratamiento de datos (obligatoria).

**No** se solicita documento, telefono, direccion ni datos sensibles.

## Consentimiento (demo)

Antes de iniciar, el widget muestra un aviso corto: canal de orientacion, no
enviar datos sensibles, la conversacion puede registrarse para trazabilidad y
al continuar se acepta el tratamiento de datos segun la politica institucional
correspondiente. El texto es demostrativo; **no afirma cumplimiento juridico
total** y queda pendiente de revision juridica.

El backend exige el consentimiento: `StartConversation` rechaza con
`Consent is required.` si `consentAccepted` no es verdadero. Se persiste:

- `Conversation.ConsentAcceptedAtUtc`
- `Conversation.ConsentVersion` (por defecto `demo-v1`)
- `Conversation.Topic`

y se emite auditoria `conversation.consent_accepted` (solo version y tema,
nunca PII).

## Categorias de atencion

Catalogo fijo del widget:

- Orientacion general
- Tramite
- PQRS
- Seguimiento
- Otro

El tema se guarda en `Conversation.Topic` y se muestra en la cola de la
consola.

## Encuesta de satisfaccion

Al cerrar la conversacion desde la consola, el widget ofrece una encuesta:

- Calificacion 1 a 5 (obligatoria).
- Comentario opcional, maximo 500 caracteres.

Endpoint publico (sin token de agente):

```
POST /api/conversations/{conversationId}/feedback
{ "rating": 5, "comment": "Atencion clara" }
```

Reglas:

- Solo se permite si la conversacion esta **cerrada** (`409` si no).
- `rating` fuera de 1..5 -> `400`.
- Comentario > 500 -> `400`.
- Un solo feedback por conversacion, garantizado por indice unico (`409` si
  ya existe).
- Auditoria `conversation.feedback_submitted` con `rating` y `feedbackId`;
  **el comentario nunca se registra en auditoria ni en logs**.

La consola muestra la calificacion de las conversaciones cerradas en la cola.

## Transcripcion local

Tras el cierre, el ciudadano puede copiar o descargar la transcripcion como
`.txt` generado en el navegador. Solo incluye mensajes visibles; **nunca**
notas internas, etiquetas internas, respuestas rapidas como configuracion ni
tokens. Formato:

```
Chat institucional demo
Fecha: <fecha local>
Ciudadano: <nombre o Anonimo>
Estado: <estado>
Mensajes visibles:
[Ciudadano] ...
[Funcionario] ...
```

## Separacion ciudadano / interno

- El widget solo consume metodos publicos del hub y el endpoint publico de
  feedback.
- Notas internas, etiquetas internas y respuestas rapidas siguen siendo
  exclusivas de la consola con token de agente.
- El endpoint de feedback no expone notas ni etiquetas internas.

## Limitaciones

- Sin autenticacion institucional (Entra ID/OIDC) ni TLS real.
- Consentimiento y textos son demostrativos, pendientes de revision juridica.
- El catalogo de categorias es fijo en el frontend.
- No hay retencion ni anonimizacion automatica.

## Como mostrarlo en demo

1. `.\scripts\demo\start-demo.ps1` y `.\scripts\demo\check-demo.ps1`.
2. Abrir `http://localhost:5174` (pagina institucional).
3. Abrir el widget, elegir tema, aceptar el aviso e iniciar como ciudadano
   demo (sin datos reales).
4. Atender desde la consola (`http://localhost:5173`), responder y cerrar.
5. En el widget, enviar la encuesta y descargar la transcripcion.

No usar datos personales sensibles reales.

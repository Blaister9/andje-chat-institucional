# Dashboard de feedback en consola (fase 13)

Aprovecha el feedback ciudadano ya persistido (`ConversationFeedback`, fase 12)
para dar visibilidad operativa de satisfaccion en la consola de agentes. No
crea tablas ni migraciones nuevas y no toca el widget.

## Que muestra el dashboard

En las tarjetas superiores de la consola:

- **Satisfaccion**: promedio de calificaciones (1 cifra decimal) con estrellas
  y porcentaje de encuestas positivas (rating >= 4). Muestra "Sin datos" si
  aun no hay encuestas.
- **Encuestas**: cantidad total de encuestas recibidas.

Las metricas provienen de `/api/console/summary` (endpoint interno con token de
agente):

```text
feedbackCount        conteo de encuestas
averageRating        promedio 1..5 (nulo si no hay encuestas)
positiveFeedbackCount conteo de rating >= 4
positiveFeedbackRate  porcentaje positivo (nulo si no hay encuestas)
```

## Rating y comentario por conversacion

En la cola, cada conversacion cerrada con encuesta muestra sus estrellas.

En el detalle de conversacion, el bloque **"Encuesta ciudadana"** muestra:

- calificacion (estrellas + `n/5`),
- comentario del ciudadano (si existe),
- fecha de la encuesta,
- estado vacio "Esta conversacion aun no tiene encuesta" cuando esta cerrada y
  sin feedback.

El comentario se renderiza como **texto plano** (JSX/`textContent`), nunca con
`dangerouslySetInnerHTML`.

## Filtro por calificacion

La cola incluye un selector de calificacion que filtra **en memoria** sobre la
lista ya cargada:

- Todas las calificaciones
- 5 / 4 / 3 / 2 / 1 estrellas
- Sin encuesta (cerradas sin feedback)

Se combina con los filtros de estado y la busqueda existentes.

## Seguridad y privacidad

- El comentario (`feedbackComment`) **solo** se devuelve en endpoints internos
  `/api/console/*` con token de agente. Nunca en endpoints publicos del widget.
- El endpoint publico de feedback y el historial del widget **no** exponen el
  comentario.
- El comentario **no** se registra en logs ni en auditoria (la auditoria de
  `conversation.feedback_submitted` sigue guardando solo `rating` y
  `feedbackId`).
- No se agregan datos sensibles nuevos ni migraciones.

## Limitaciones

- Sin analitica historica avanzada ni series de tiempo.
- Sin filtros por fecha.
- Sin exportacion de encuestas.
- El promedio es global sobre todas las encuestas persistidas (no segmentado
  por tema, agente ni periodo).

## Como mostrarlo en demo

1. Cerrar una conversacion y enviar la encuesta desde el widget (rating +
   comentario).
2. En la consola, "Actualizar": el dashboard suma la encuesta y muestra el
   promedio.
3. Abrir la conversacion cerrada: el bloque "Encuesta ciudadana" muestra
   estrellas y comentario.
4. Usar el filtro de calificacion (por ejemplo "5 estrellas" y "Sin encuesta").

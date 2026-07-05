# Línea base de seguridad y privacidad — Chat institucional ANDJE

> Fase 00. Este documento fija principios y controles mínimos que las fases
> siguientes deben respetar. No sustituye la revisión del oficial de
> seguridad ni del área jurídica de la entidad.

## Marco normativo de referencia (Colombia)

- **Ley 1581 de 2012** y Decreto 1377 de 2013 — protección de datos
  personales (habeas data).
- **Ley 1712 de 2014** — transparencia y acceso a la información pública.
- Lineamientos de la **Política de Gobierno Digital** (MinTIC) y del Modelo
  de Seguridad y Privacidad de la Información (MSPI).

## Principios

1. **Minimización de datos.** El chat funciona con visitante anónimo. Solo se
   piden datos personales opcionales (nombre a mostrar, correo para
   seguimiento) y nunca datos sensibles. El widget no usa cookies de
   rastreo ni analítica de terceros.
2. **Soberanía del dato.** Las conversaciones se almacenan únicamente en
   infraestructura controlada por la entidad (motivo central para abandonar
   el chat de terceros). Prohibido enviar contenido de conversaciones a
   servicios externos sin ADR que lo autorice y revisión jurídica previa.
3. **Trazabilidad y auditoría.** Todo evento relevante queda en `AuditEvent`
   (solo inserción). Mensajes inmutables. Los agentes se identifican con
   cuenta institucional individual: no hay cuentas compartidas.
4. **Consentimiento informado.** Antes de iniciar la conversación, el widget
   deberá mostrar el aviso de privacidad y el tratamiento de datos
   (pendiente: texto oficial del área jurídica).
5. **Nada de secretos en el repositorio.** Configuración sensible por
   variables de entorno (`.env` local ignorado por git). El único valor por
   defecto versionado es la contraseña de PostgreSQL de desarrollo local,
   que no protege ningún dato real.

## Controles por fase

### Vigentes (fase 00)

- Sin secretos ni credenciales en el código.
- CORS restringido por lista explícita de orígenes (`Cors:AllowedOrigins`),
  no `*`.
- Superficie mínima: solo `/health` y `/hubs/chat` expuestos.
- Entorno declarado de desarrollo; ninguna configuración asume producción.

### Bloqueantes antes de exponer el sistema fuera de localhost

- [ ] **Autenticación de agentes** (la consola y el hub hoy son anónimos).
      Objetivo: OpenID Connect contra el proveedor de identidad institucional.
- [ ] **Autorización por roles** (`Agent`, `Supervisor`, `Admin`).
- [ ] **TLS en todos los canales** (HTTPS/WSS), hoy HTTP plano local.
- [ ] Token efímero para visitantes del widget (limitar el hub a sesiones
      emitidas por la API).
- [ ] Rate limiting y límites de tamaño de mensaje en API y hub.
- [ ] Sanitización/escape del contenido de mensajes en consola y widget
      (defensa XSS; el contenido del ciudadano es entrada no confiable).
- [ ] Logging estructurado **sin datos personales ni contenido de
      conversaciones** en los logs.
- [ ] Política de retención y anonimización de conversaciones, con plazos
      definidos por el área jurídica.
- [ ] Respaldo y cifrado en reposo de la base de datos.

## Gestión de vulnerabilidades

- Dependencias fijadas por lockfiles (`package-lock.json`) y versiones
  explícitas de NuGet; actualización revisada por PR.
- Pendiente (fase de CI): análisis automático de dependencias vulnerables
  (`dotnet list package --vulnerable`, `npm audit`) en la tubería.

## Datos personales previstos y su tratamiento

| Dato                    | Dónde vive                  | Tratamiento                             |
| ----------------------- | --------------------------- | --------------------------------------- |
| Nombre a mostrar (opc.) | `Visitor.DisplayName`       | Solo para la conversación               |
| Correo (opc.)           | `Visitor.ContactEmail`      | Solo seguimiento; nunca marketing       |
| Contenido de mensajes   | `Message.Content`           | Puede contener datos que el ciudadano   |
|                         |                             | decida escribir; retención limitada,    |
|                         |                             | acceso solo a agentes autorizados       |
| Correo institucional    | `Agent.Email`               | Identificación laboral del agente       |

No se recolectan: identificación oficial, teléfono, ubicación precisa,
datos sensibles (Ley 1581, art. 5), ni huella del navegador.

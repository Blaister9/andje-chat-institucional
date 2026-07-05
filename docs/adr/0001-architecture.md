# ADR 0001 — Arquitectura inicial del chat institucional

- **Estado:** aceptado
- **Fecha:** 2026-07-05
- **Fase:** 00 — fundación del proyecto

## Contexto

La entidad usa hoy una solución de chat de terceros (tipo tawk.to). Eso
implica que las conversaciones con ciudadanos —que pueden contener datos
personales— viven en infraestructura externa, sin trazabilidad ni auditoría
bajo control institucional. Se decide construir un prototipo propio, con
mensajería en tiempo real, consola de agentes y widget embebible, preparado
para IA asistida en el futuro pero sin implementarla aún.

## Decisiones

### 1. Monorepo

Un solo repositorio con backend, consola y widget.

- Un prototipo con un equipo pequeño se beneficia de PRs atómicos que cruzan
  backend y frontend, y de una sola tubería de CI futura.
- Los contratos (DTOs, eventos del hub) evolucionan juntos; separar repos a
  esta escala solo agrega fricción.

### 2. Backend: .NET 8 Web API + SignalR

- .NET 8 es LTS, con soporte hasta noviembre de 2026 y ruta de migración
  directa a .NET 10 LTS.
- SignalR viene incluido en ASP.NET Core (sin dependencias adicionales),
  maneja WebSockets con fallback automático a SSE/long-polling, y tiene
  cliente TypeScript oficial (`@microsoft/signalr`) que usarán el widget y la
  consola.
- Alternativa descartada: WebSockets crudos (reimplementar reconexión,
  multiplexación y fallback sin beneficio para este caso).

### 3. Consola interna: React (sobre Angular)

- **Curva de entrada y talento:** React domina el mercado local; para una
  entidad pública que rota contratistas, encontrar perfiles React es más
  fácil que Angular.
- **Peso adecuado al problema:** la consola es una SPA de un dominio acotado
  (cola + conversación). Angular aporta un marco completo (DI, módulos,
  RxJS) que aquí sería sobre-ingeniería; React + Vite deja la base mínima y
  crece por composición.
- **Coherencia con el widget:** el widget es un Web Component sin framework;
  mantener React (librería, no framework) reduce la distancia conceptual
  entre ambas piezas.
- Angular sigue siendo válido si la entidad ya tuviera estándar corporativo
  Angular; no es el caso conocido.

### 4. Widget público: Web Component (vanilla TS)

- Debe embeberse en portales que no controlamos (Drupal, WordPress, .NET
  legado). Un custom element con Shadow DOM aísla estilos y no impone
  framework ni versión al portal anfitrión.
- Se distribuye como un único archivo IIFE (`andje-chat-widget.js`):
  `<script>` + etiqueta HTML, nada más.
- Alternativa descartada: iframe (más aislamiento pero peor UX móvil y
  comunicación más rígida); puede reconsiderarse si aparecen requisitos de
  aislamiento fuertes.

### 5. Base de datos: PostgreSQL (sobre SQL Server)

- **Costo:** sin licenciamiento; relevante para presupuesto público y para
  levantar N entornos sin restricciones.
- **Operación en contenedores:** imagen oficial ligera (`postgres:16-alpine`),
  estándar de facto en Docker Compose y en los principales proveedores de
  nube con oferta gubernamental.
- **Funcionalidad útil al dominio:** JSONB para metadatos de mensajes y
  eventos de auditoría, full-text search con diccionario en español para
  búsqueda en transcripciones.
- SQL Server sería preferible solo si la entidad exigiera alinearse a un
  estándar corporativo Microsoft con licencias ya pagadas; aun así, EF Core
  (fase siguiente) permite cambiar de proveedor con costo acotado.

### 6. Tiempo real preparado, dominio después

En esta fase el hub `/hubs/chat` existe y acepta conexiones (verificado por
smoke test), pero no implementa mensajería. La persistencia se pospone a la
fase siguiente (EF Core + migraciones); PostgreSQL ya queda provisionado en
Compose para que esa fase no toque infraestructura.

### 7. Docker Compose como entorno local único

`docker compose up --build` levanta db + api + consola + widget. Los
frontends corren en contenedores `node:22-alpine` con Vite en modo dev
(volúmenes montados, HMR); no hay imágenes de producción porque producción
está fuera de alcance.

## Consecuencias

- Cualquier agente/desarrollador levanta el entorno con un comando y sin
  credenciales externas.
- La elección de PostgreSQL condiciona la fase de persistencia a
  `Npgsql.EntityFrameworkCore.PostgreSQL`.
- El widget no podrá usar APIs específicas de React/Angular; todo lo
  compartido con la consola deberá extraerse a paquetes TS neutrales.
- Al no haber autenticación aún, el hub es anónimo: **bloqueante a resolver
  antes de cualquier exposición fuera de localhost** (ver
  [privacy-security-baseline.md](../privacy-security-baseline.md)).

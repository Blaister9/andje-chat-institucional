# HTTPS and forwarded headers for controlled demos

Esta fase prepara el backend para demos controladas detras de un proxy,
gateway o tunel que termina TLS fuera de la aplicacion. No es una receta de
produccion ni reemplaza revision institucional de seguridad.

## Modos de demo

### Modo 1: localhost HTTP

- Recomendado para desarrollo.
- No requiere forwarded headers.
- No requiere HTTPS.
- Defaults:
  - `ForwardedHeaders:Enabled=false`
  - `Https:RequireHttps=false`
  - `Https:UseHsts=false`

### Modo 2: LAN HTTP controlada

- Solo en red controlada.
- No usar datos reales.
- Configurar origenes explicitos con `DEMO_ALLOWED_ORIGINS`.
- No dejar corriendo despues de la demo.
- No habilitar HSTS ni redireccion HTTPS si no hay HTTPS real.

Ejemplo local no versionado de `.env.demo`:

```text
DEMO_API_URL=http://192.168.1.50:8080
DEMO_ALLOWED_ORIGINS=http://192.168.1.50:5173,http://192.168.1.50:5174
```

### Modo 3: detras de proxy HTTPS

- El proxy termina TLS.
- El backend recibe trafico interno HTTP desde el proxy.
- El proxy debe enviar `X-Forwarded-Proto=https`.
- Opcionalmente puede enviar `X-Forwarded-For` y `X-Forwarded-Host`.
- Habilite forwarded headers solo con proxies o redes conocidas.
- Configure CORS con origenes HTTPS explicitos.
- Evalúe HSTS solo si la ruta HTTPS esta correctamente configurada.

Ejemplo local no versionado de `.env.demo`:

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

`ANDJE_HTTPS_REQUIRE=false` es normal cuando TLS termina en el proxy y el salto
proxy-backend es HTTP interno. Active redireccion HTTPS solo si el entorno lo
soporta y fue probado.

## Configuracion

`appsettings.json` define defaults seguros:

```json
{
  "ForwardedHeaders": {
    "Enabled": false,
    "ForwardLimit": 1,
    "KnownProxies": [],
    "KnownNetworks": []
  },
  "Https": {
    "RequireHttps": false,
    "UseHsts": false
  }
}
```

Variables soportadas por Docker Compose:

- `ANDJE_FORWARDED_HEADERS_ENABLED`
- `ANDJE_FORWARDED_HEADERS_KNOWN_PROXIES`
- `ANDJE_FORWARDED_HEADERS_KNOWN_NETWORKS`
- `ANDJE_FORWARDED_HEADERS_FORWARD_LIMIT`
- `ANDJE_HTTPS_REQUIRE`
- `ANDJE_HTTPS_HSTS`

Listas de proxies/redes aceptan coma o punto y coma:

```text
ANDJE_FORWARDED_HEADERS_KNOWN_PROXIES=192.168.1.10,192.168.1.11
ANDJE_FORWARDED_HEADERS_KNOWN_NETWORKS=192.168.1.0/24
```

## Seguridad

- No confiar en cualquier forwarded header.
- No usar `*` en CORS.
- No subir certificados, `.pfx`, `.pem`, `.key` ni `.crt`.
- No subir `.env` ni `.env.demo`.
- No usar datos reales.
- Sin Entra ID/OIDC.
- Sin TLS extremo a extremo si el proxy no esta correctamente configurado.
- Requiere revision de seguridad institucional antes de un despliegue real.

Fuera de `Development`/`Test`, si `ForwardedHeaders:Enabled=true` y no hay
`KnownProxies` ni `KnownNetworks` validos, la aplicacion falla al arrancar.

## Rate limiting e IP real

El rate limiting particiona por `HttpContext.Connection.RemoteIpAddress`.
Cuando `UseForwardedHeaders` esta habilitado y el proxy es conocido, ASP.NET
Core actualiza esa IP con `X-Forwarded-For` antes de ejecutar rate limiting.

Sin forwarded headers configurados, el limitador ve la IP directa del proxy o
del cliente local.

## HSTS y redireccion HTTPS

- `Https:RequireHttps=true` activa `UseHttpsRedirection()`.
- `Https:UseHsts=true` activa HSTS solo fuera de `Development` y `Test`.
- Por defecto ambos estan apagados para no romper localhost/LAN HTTP.
- No se configuran certificados reales en esta fase.

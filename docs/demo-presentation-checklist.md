# Demo presentation checklist

## Antes

- Actualizar base: `git checkout develop && git pull --ff-only origin develop`.
- Detener stack previo: `docker compose down`.
- Levantar demo: `.\scripts\demo\start-demo.ps1`.
- Validar demo: `.\scripts\demo\check-demo.ps1`.
- Limpiar datos si se requiere: `.\scripts\demo\reset-demo-data.ps1`.
- Probar login de consola con `Funcionario Demo` / `andje-agent-local`.
- Probar widget con una conversacion corta.
- Confirmar que no se usaran datos reales.
- Confirmar que no se expondran puertos en una red no controlada.

## Durante

- Mostrar flujo ciudadano desde `http://localhost:5174`.
- Mostrar consola de funcionario desde `http://localhost:5173`.
- Enviar mensaje ciudadano.
- Responder desde consola.
- Cerrar conversacion.
- Mostrar cierre en widget.
- Mostrar auditoria tecnica con consulta SQL.
- Mostrar CI solo si hay PR o checks recientes disponibles.
- Repetir que es demo local/LAN, no produccion.

## Despues

- Detener demo: `.\scripts\demo\stop-demo.ps1`.
- Limpiar datos si hubo informacion sensible accidental.
- Registrar feedback funcional y tecnico.
- No dejar la demo corriendo en red.
- No versionar `.env.demo`, logs, dumps ni capturas con datos reales.

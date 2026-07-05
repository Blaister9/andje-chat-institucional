export function App() {
  return (
    <div className="layout">
      <header className="header">
        <h1>Consola de agentes</h1>
        <span className="subtitle">Chat institucional — ANDJE</span>
      </header>

      <main className="panels">
        <section className="panel" aria-label="Cola de conversaciones">
          <h2>Cola de conversaciones</h2>
          <p className="placeholder">
            Aquí se listarán las conversaciones entrantes de los ciudadanos.
            Disponible en la fase de mensajería en tiempo real.
          </p>
        </section>

        <section className="panel" aria-label="Conversación activa">
          <h2>Conversación activa</h2>
          <p className="placeholder">
            Aquí el agente atenderá la conversación seleccionada.
            Disponible en la fase de mensajería en tiempo real.
          </p>
        </section>
      </main>

      <footer className="footer">
        Prototipo interno — fase 00: fundación del proyecto. No apto para producción.
      </footer>
    </div>
  );
}

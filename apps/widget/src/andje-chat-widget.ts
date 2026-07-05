/**
 * Web Component embebible del chat institucional.
 *
 * Fase 00: solo interfaz placeholder (botón flotante + panel), sin conexión
 * al backend. La conexión SignalR se agrega en la fase de mensajería.
 *
 * Uso en un portal externo:
 *   <script src="https://<host>/andje-chat-widget.js"></script>
 *   <andje-chat-widget titulo="Chat institucional"></andje-chat-widget>
 */
export class AndjeChatWidget extends HTMLElement {
  static readonly tagName = 'andje-chat-widget';

  #abierto = false;
  #panel!: HTMLDivElement;
  #boton!: HTMLButtonElement;

  connectedCallback(): void {
    const titulo = this.getAttribute('titulo') ?? 'Chat institucional';
    const shadow = this.attachShadow({ mode: 'open' });

    shadow.innerHTML = `
      <style>
        :host {
          position: fixed;
          right: 1.25rem;
          bottom: 1.25rem;
          z-index: 2147483000;
          font-family: 'Segoe UI', system-ui, sans-serif;
        }
        .boton {
          width: 56px;
          height: 56px;
          border: none;
          border-radius: 50%;
          background: #10316b;
          color: #ffffff;
          font-size: 1.5rem;
          cursor: pointer;
          box-shadow: 0 4px 12px rgba(16, 49, 107, 0.4);
        }
        .panel {
          display: none;
          position: absolute;
          right: 0;
          bottom: 72px;
          width: 320px;
          background: #ffffff;
          border: 1px solid #d9e2ec;
          border-radius: 12px;
          box-shadow: 0 8px 24px rgba(31, 41, 51, 0.2);
          overflow: hidden;
        }
        .panel.abierto {
          display: block;
        }
        .encabezado {
          background: #10316b;
          color: #ffffff;
          padding: 0.75rem 1rem;
          font-size: 0.95rem;
          font-weight: 600;
        }
        .cuerpo {
          padding: 1rem;
          font-size: 0.875rem;
          color: #52606d;
        }
      </style>
      <div class="panel" role="dialog" aria-label="${titulo}">
        <div class="encabezado">${titulo}</div>
        <div class="cuerpo">
          El chat estará disponible próximamente. Esta es una versión de
          prueba sin conexión.
        </div>
      </div>
      <button class="boton" type="button" aria-label="Abrir chat" aria-expanded="false">💬</button>
    `;

    this.#panel = shadow.querySelector<HTMLDivElement>('.panel')!;
    this.#boton = shadow.querySelector<HTMLButtonElement>('.boton')!;
    this.#boton.addEventListener('click', () => this.#alternar());
  }

  #alternar(): void {
    this.#abierto = !this.#abierto;
    this.#panel.classList.toggle('abierto', this.#abierto);
    this.#boton.setAttribute('aria-expanded', String(this.#abierto));
  }
}

if (!customElements.get(AndjeChatWidget.tagName)) {
  customElements.define(AndjeChatWidget.tagName, AndjeChatWidget);
}

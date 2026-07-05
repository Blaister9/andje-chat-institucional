/**
 * Web Component embebible del chat institucional.
 *
 * Fase 01: flujo real de mensajería vía SignalR, sin persistencia (el
 * backend guarda las conversaciones en memoria).
 *
 * Uso en un portal externo:
 *   <script src="https://<host>/andje-chat-widget.js"></script>
 *   <andje-chat-widget titulo="Chat institucional" api-base="https://<api>"></andje-chat-widget>
 */
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';

interface ConversationDto {
  id: string;
  status: string;
  visitorDisplayName: string | null;
  startedAt: string;
}

interface ChatMessageDto {
  id: string;
  conversationId: string;
  senderType: 'Visitor' | 'Agent';
  content: string;
  sentAt: string;
}

export class AndjeChatWidget extends HTMLElement {
  static readonly tagName = 'andje-chat-widget';

  #abierto = false;
  #conexion: HubConnection | null = null;
  #conversacion: ConversationDto | null = null;
  #mensajesPintados = new Set<string>();

  #panel!: HTMLDivElement;
  #boton!: HTMLButtonElement;
  #vistaInicio!: HTMLFormElement;
  #vistaChat!: HTMLDivElement;
  #listaMensajes!: HTMLDivElement;
  #formularioMensaje!: HTMLFormElement;
  #campoMensaje!: HTMLInputElement;
  #lineaEstado!: HTMLParagraphElement;

  get #apiBase(): string {
    return this.getAttribute('api-base') ?? 'http://localhost:8080';
  }

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
          width: 340px;
          background: #ffffff;
          border: 1px solid #d9e2ec;
          border-radius: 12px;
          box-shadow: 0 8px 24px rgba(31, 41, 51, 0.2);
          overflow: hidden;
        }
        .panel.abierto { display: block; }
        .encabezado {
          background: #10316b;
          color: #ffffff;
          padding: 0.75rem 1rem;
          font-size: 0.95rem;
          font-weight: 600;
        }
        .cuerpo { padding: 0.75rem 1rem 1rem; }
        .inicio label {
          display: block;
          font-size: 0.8rem;
          color: #52606d;
          margin-bottom: 0.35rem;
        }
        .inicio input, .envio input {
          width: 100%;
          box-sizing: border-box;
          padding: 0.5rem 0.6rem;
          border: 1px solid #d9e2ec;
          border-radius: 6px;
          font: inherit;
        }
        .inicio button, .envio button {
          border: none;
          border-radius: 6px;
          background: #10316b;
          color: #ffffff;
          font: inherit;
          cursor: pointer;
          padding: 0.5rem 0.9rem;
        }
        .inicio button { margin-top: 0.6rem; width: 100%; }
        .inicio button:disabled, .envio button:disabled { opacity: 0.6; cursor: default; }
        .chat { display: none; }
        .chat.activo { display: block; }
        .mensajes {
          height: 260px;
          overflow-y: auto;
          display: flex;
          flex-direction: column;
          gap: 0.4rem;
          padding: 0.25rem 0;
        }
        .mensaje {
          max-width: 85%;
          padding: 0.45rem 0.65rem;
          border-radius: 10px;
          font-size: 0.85rem;
          white-space: pre-wrap;
          word-break: break-word;
        }
        .mensaje.visitor {
          align-self: flex-end;
          background: #10316b;
          color: #ffffff;
          border-bottom-right-radius: 2px;
        }
        .mensaje.agent {
          align-self: flex-start;
          background: #eef2f7;
          color: #1f2933;
          border-bottom-left-radius: 2px;
        }
        .envio {
          display: flex;
          gap: 0.4rem;
          margin-top: 0.6rem;
        }
        .estado {
          margin: 0.5rem 0 0;
          font-size: 0.75rem;
          color: #52606d;
          min-height: 1em;
        }
        .estado.error { color: #ab091e; }
      </style>
      <div class="panel" role="dialog" aria-label="${titulo}">
        <div class="encabezado">${titulo}</div>
        <div class="cuerpo">
          <form class="inicio">
            <label for="nombre">Nombre (opcional)</label>
            <input id="nombre" name="nombre" type="text" maxlength="80"
                   autocomplete="off" placeholder="¿Cómo quiere que le llamemos?" />
            <button type="submit">Iniciar chat</button>
          </form>
          <div class="chat">
            <div class="mensajes" aria-live="polite"></div>
            <form class="envio">
              <input name="mensaje" type="text" maxlength="2000" autocomplete="off"
                     placeholder="Escriba su mensaje…" aria-label="Mensaje" />
              <button type="submit">Enviar</button>
            </form>
          </div>
          <p class="estado"></p>
        </div>
      </div>
      <button class="boton" type="button" aria-label="Abrir chat" aria-expanded="false">💬</button>
    `;

    this.#panel = shadow.querySelector<HTMLDivElement>('.panel')!;
    this.#boton = shadow.querySelector<HTMLButtonElement>('.boton')!;
    this.#vistaInicio = shadow.querySelector<HTMLFormElement>('.inicio')!;
    this.#vistaChat = shadow.querySelector<HTMLDivElement>('.chat')!;
    this.#listaMensajes = shadow.querySelector<HTMLDivElement>('.mensajes')!;
    this.#formularioMensaje = shadow.querySelector<HTMLFormElement>('.envio')!;
    this.#campoMensaje = this.#formularioMensaje.querySelector<HTMLInputElement>('input')!;
    this.#lineaEstado = shadow.querySelector<HTMLParagraphElement>('.estado')!;

    this.#boton.addEventListener('click', () => this.#alternar());
    this.#vistaInicio.addEventListener('submit', (e) => {
      e.preventDefault();
      void this.#iniciarConversacion();
    });
    this.#formularioMensaje.addEventListener('submit', (e) => {
      e.preventDefault();
      void this.#enviarMensaje();
    });
  }

  disconnectedCallback(): void {
    void this.#conexion?.stop();
  }

  #alternar(): void {
    this.#abierto = !this.#abierto;
    this.#panel.classList.toggle('abierto', this.#abierto);
    this.#boton.setAttribute('aria-expanded', String(this.#abierto));
  }

  async #conectar(): Promise<HubConnection> {
    if (this.#conexion) {
      return this.#conexion;
    }

    const conexion = new HubConnectionBuilder()
      .withUrl(`${this.#apiBase}/hubs/chat`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    conexion.on('MessageReceived', (mensaje: ChatMessageDto) => {
      if (mensaje.conversationId === this.#conversacion?.id) {
        this.#pintarMensaje(mensaje);
      }
    });

    conexion.onreconnecting(() => this.#estado('Reconectando…'));
    conexion.onreconnected(() => {
      this.#estado('');
      void this.#repintarHistorial();
    });

    await conexion.start();
    this.#conexion = conexion;
    return conexion;
  }

  async #iniciarConversacion(): Promise<void> {
    const boton = this.#vistaInicio.querySelector('button')!;
    boton.disabled = true;
    this.#estado('Conectando…');
    try {
      const conexion = await this.#conectar();
      const nombre =
        this.#vistaInicio.querySelector<HTMLInputElement>('input')!.value.trim() || null;
      this.#conversacion = await conexion.invoke<ConversationDto>('StartConversation', {
        displayName: nombre,
      });
      this.#vistaInicio.style.display = 'none';
      this.#vistaChat.classList.add('activo');
      this.#estado('');
      this.#campoMensaje.focus();
    } catch {
      this.#estado('No fue posible conectar con el chat. Intente de nuevo.', true);
    } finally {
      boton.disabled = false;
    }
  }

  async #enviarMensaje(): Promise<void> {
    const contenido = this.#campoMensaje.value.trim();
    if (!contenido || !this.#conexion || !this.#conversacion) {
      return;
    }
    try {
      // El mensaje se pinta cuando el servidor lo confirma (eco del grupo).
      await this.#conexion.invoke('SendVisitorMessage', {
        conversationId: this.#conversacion.id,
        content: contenido,
      });
      this.#campoMensaje.value = '';
      this.#estado('');
    } catch {
      this.#estado('El mensaje no pudo enviarse.', true);
    }
  }

  async #repintarHistorial(): Promise<void> {
    if (!this.#conexion || !this.#conversacion) {
      return;
    }
    const historial = await this.#conexion.invoke<ChatMessageDto[]>(
      'JoinConversation',
      this.#conversacion.id,
    );
    this.#listaMensajes.replaceChildren();
    this.#mensajesPintados.clear();
    historial.forEach((m) => this.#pintarMensaje(m));
  }

  #pintarMensaje(mensaje: ChatMessageDto): void {
    if (this.#mensajesPintados.has(mensaje.id)) {
      return;
    }
    this.#mensajesPintados.add(mensaje.id);

    const burbuja = document.createElement('div');
    burbuja.className = `mensaje ${mensaje.senderType.toLowerCase()}`;
    // textContent (nunca innerHTML): el contenido es entrada no confiable.
    burbuja.textContent = mensaje.content;
    this.#listaMensajes.appendChild(burbuja);
    this.#listaMensajes.scrollTop = this.#listaMensajes.scrollHeight;
  }

  #estado(texto: string, esError = false): void {
    this.#lineaEstado.textContent = texto;
    this.#lineaEstado.classList.toggle('error', esError);
  }
}

if (!customElements.get(AndjeChatWidget.tagName)) {
  customElements.define(AndjeChatWidget.tagName, AndjeChatWidget);
}

/**
 * Web Component embebible del chat institucional (experiencia ciudadana).
 *
 * Fase 12: pantalla de inicio con consentimiento y categoria, timeline
 * mejorado, estados claros, cierre institucional, encuesta de satisfaccion y
 * transcripcion local. Solo muestra contenido visible al ciudadano: nunca
 * notas internas, etiquetas internas ni configuracion de agente.
 */
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';

interface ConversationDto {
  id: string;
  status: 'Pending' | 'Active' | 'Closed';
  visitorDisplayName: string | null;
  startedAt: string;
  updatedAtUtc: string;
  closedAtUtc: string | null;
  topic: string | null;
}

interface ChatMessageDto {
  id: string;
  conversationId: string;
  senderType: 'Visitor' | 'Agent';
  content: string;
  sentAt: string;
}

const STORAGE_KEY = 'andje-chat.conversationId';
const CONSENT_VERSION = 'demo-v1';
const TOPICS = ['Orientacion general', 'Tramite', 'PQRS', 'Seguimiento', 'Otro'];
const CLOSED_TEXT =
  'Esta conversacion fue cerrada por el equipo de atencion. Puedes descargar la transcripcion o iniciar una nueva consulta.';

export class AndjeChatWidget extends HTMLElement {
  static readonly tagName = 'andje-chat-widget';

  #abierto = false;
  #conexion: HubConnection | null = null;
  #conversacion: ConversationDto | null = null;
  #mensajesPintados = new Set<string>();
  #transcripcion: ChatMessageDto[] = [];
  #feedbackEnviado = false;

  #panel!: HTMLDivElement;
  #boton!: HTMLButtonElement;
  #estadoConexion!: HTMLSpanElement;
  #vistaInicio!: HTMLFormElement;
  #consentCheck!: HTMLInputElement;
  #topicSelect!: HTMLSelectElement;
  #vistaChat!: HTMLDivElement;
  #bannerEstado!: HTMLDivElement;
  #listaMensajes!: HTMLDivElement;
  #formularioMensaje!: HTMLFormElement;
  #campoMensaje!: HTMLInputElement;
  #botonEnviar!: HTMLButtonElement;
  #vistaCierre!: HTMLDivElement;
  #formularioFeedback!: HTMLFormElement;
  #comentarioFeedback!: HTMLTextAreaElement;
  #graciasFeedback!: HTMLParagraphElement;
  #ratingSeleccionado = 0;
  #lineaEstado!: HTMLParagraphElement;

  get #apiBase(): string {
    return this.getAttribute('api-base') ?? 'http://localhost:8080';
  }

  connectedCallback(): void {
    const titulo = this.getAttribute('titulo') ?? 'Chat institucional';
    const shadow = this.attachShadow({ mode: 'open' });
    shadow.innerHTML = this.#plantilla(titulo);

    this.#panel = shadow.querySelector('.panel')!;
    this.#boton = shadow.querySelector('.boton')!;
    this.#estadoConexion = shadow.querySelector('.conexion')!;
    this.#vistaInicio = shadow.querySelector('.inicio')!;
    this.#consentCheck = shadow.querySelector('.consent-check')!;
    this.#topicSelect = shadow.querySelector('.topic')!;
    this.#vistaChat = shadow.querySelector('.chat')!;
    this.#bannerEstado = shadow.querySelector('.banner')!;
    this.#listaMensajes = shadow.querySelector('.mensajes')!;
    this.#formularioMensaje = shadow.querySelector('.envio')!;
    this.#campoMensaje = this.#formularioMensaje.querySelector('input')!;
    this.#botonEnviar = this.#formularioMensaje.querySelector('button')!;
    this.#vistaCierre = shadow.querySelector('.cierre')!;
    this.#formularioFeedback = shadow.querySelector('.feedback')!;
    this.#comentarioFeedback = shadow.querySelector('.feedback-comment')!;
    this.#graciasFeedback = shadow.querySelector('.feedback-gracias')!;
    this.#lineaEstado = shadow.querySelector('.estado')!;

    this.#boton.addEventListener('click', () => this.#alternar());
    this.#vistaInicio.addEventListener('submit', (e) => {
      e.preventDefault();
      void this.#iniciarConversacion();
    });
    this.#consentCheck.addEventListener('change', () => this.#actualizarBotonInicio());
    this.#formularioMensaje.addEventListener('submit', (e) => {
      e.preventDefault();
      void this.#enviarMensaje();
    });
    this.#formularioFeedback.addEventListener('submit', (e) => {
      e.preventDefault();
      void this.#enviarFeedback();
    });
    shadow.querySelector('.nueva-conversacion')!.addEventListener('click', () =>
      this.#reiniciarConversacion(),
    );
    shadow.querySelector('.copiar')!.addEventListener('click', () => void this.#copiarTranscripcion());
    shadow.querySelector('.descargar')!.addEventListener('click', () => this.#descargarTranscripcion());
    shadow.querySelectorAll<HTMLButtonElement>('.star').forEach((star) =>
      star.addEventListener('click', () => this.#seleccionarRating(Number(star.dataset.value))),
    );

    this.#actualizarBotonInicio();
    this.#estadoConexionUi('sin-conexion');
    void this.#reanudarConversacion();
  }

  disconnectedCallback(): void {
    void this.#conexion?.stop();
  }

  #plantilla(titulo: string): string {
    const opciones = TOPICS.map((t) => `<option value="${t}">${t}</option>`).join('');
    const estrellas = [1, 2, 3, 4, 5]
      .map((n) => `<button type="button" class="star" data-value="${n}" aria-label="Calificacion ${n}">&#9733;</button>`)
      .join('');
    return `
      <style>
        :host {
          position: fixed; right: 1.25rem; bottom: 1.25rem; z-index: 2147483000;
          font-family: 'Segoe UI', system-ui, sans-serif;
        }
        * { box-sizing: border-box; }
        .boton {
          width: 60px; height: 60px; border: none; border-radius: 50%;
          background: #10316b; color: #fff; font-size: 1.6rem; cursor: pointer;
          box-shadow: 0 6px 18px rgba(16, 49, 107, 0.45);
        }
        .panel {
          display: none; position: absolute; right: 0; bottom: 74px; width: 360px;
          max-width: calc(100vw - 2rem); background: #fff; border-radius: 14px;
          box-shadow: 0 14px 40px rgba(31, 41, 51, 0.28); overflow: hidden;
        }
        .panel.abierto { display: block; }
        .encabezado {
          background: linear-gradient(135deg, #10316b, #1b4a99); color: #fff;
          padding: 0.85rem 1rem;
        }
        .encabezado .titulo { font-size: 0.98rem; font-weight: 700; }
        .encabezado .subtitulo { font-size: 0.72rem; opacity: 0.85; }
        .conexion {
          display: inline-flex; align-items: center; gap: 0.35rem; margin-top: 0.35rem;
          font-size: 0.68rem; opacity: 0.95;
        }
        .conexion::before {
          content: ''; width: 8px; height: 8px; border-radius: 50%; background: #9aa5b1;
        }
        .conexion.conectado::before { background: #6ee7a8; }
        .conexion.conectando::before, .conexion.reconectando::before { background: #ffd166; }
        .conexion.sin-conexion::before { background: #f28b82; }
        .cuerpo { padding: 0.85rem 1rem 1rem; }
        label { display: block; font-size: 0.78rem; color: #3e4c59; margin-bottom: 0.3rem; }
        input[type=text], select, textarea {
          width: 100%; padding: 0.5rem 0.6rem; border: 1px solid #d9e2ec;
          border-radius: 8px; font: inherit; margin-bottom: 0.7rem;
        }
        textarea { resize: vertical; min-height: 60px; }
        .intro {
          font-size: 0.78rem; color: #52606d; background: #f4f6f8;
          border-radius: 8px; padding: 0.6rem 0.7rem; margin-bottom: 0.8rem;
        }
        .consent {
          display: flex; gap: 0.5rem; align-items: flex-start; font-size: 0.74rem;
          color: #3e4c59; margin-bottom: 0.8rem;
        }
        .consent input { margin-top: 0.15rem; }
        .primario {
          border: none; border-radius: 8px; background: #10316b; color: #fff;
          font: inherit; font-weight: 600; cursor: pointer; padding: 0.55rem 0.9rem; width: 100%;
        }
        .primario:disabled { opacity: 0.5; cursor: not-allowed; }
        .secundario {
          border: 1px solid #10316b; border-radius: 8px; background: #fff; color: #10316b;
          font: inherit; cursor: pointer; padding: 0.45rem 0.7rem;
        }
        .chat, .cierre { display: none; }
        .chat.activo, .cierre.activo { display: block; }
        .banner {
          font-size: 0.74rem; padding: 0.4rem 0.6rem; border-radius: 8px;
          background: #fff3c4; color: #7c5e10; margin-bottom: 0.6rem; text-align: center;
        }
        .banner.activa { background: #d1fadf; color: #0f7b3f; }
        .banner.oculto { display: none; }
        .mensajes {
          height: 260px; overflow-y: auto; display: flex; flex-direction: column;
          gap: 0.45rem; padding: 0.25rem 0.1rem;
        }
        .fila { display: flex; flex-direction: column; max-width: 85%; }
        .fila.visitor { align-self: flex-end; align-items: flex-end; }
        .fila.agent { align-self: flex-start; align-items: flex-start; }
        .remitente { font-size: 0.62rem; color: #7b8794; margin: 0 0.2rem 0.1rem; }
        .mensaje {
          padding: 0.5rem 0.7rem; border-radius: 12px; font-size: 0.85rem;
          white-space: pre-wrap; word-break: break-word;
        }
        .fila.visitor .mensaje { background: #10316b; color: #fff; border-bottom-right-radius: 3px; }
        .fila.agent .mensaje { background: #eef2f7; color: #1f2933; border-bottom-left-radius: 3px; }
        .envio { display: flex; gap: 0.4rem; margin-top: 0.6rem; }
        .envio input { margin-bottom: 0; }
        .envio button { border: none; border-radius: 8px; background: #10316b; color: #fff; font: inherit; cursor: pointer; padding: 0 0.9rem; }
        .envio button:disabled, .envio input:disabled { opacity: 0.5; }
        .estado { margin: 0.5rem 0 0; font-size: 0.72rem; color: #52606d; min-height: 1em; }
        .estado.error { color: #ab091e; }
        .cierre-texto { font-size: 0.8rem; color: #3e4c59; margin: 0 0 0.7rem; }
        .stars { display: flex; gap: 0.2rem; margin-bottom: 0.6rem; }
        .star { background: none; border: none; font-size: 1.5rem; color: #d9e2ec; cursor: pointer; padding: 0; }
        .star.activa { color: #f2b705; }
        .acciones { display: flex; gap: 0.4rem; margin-top: 0.8rem; }
        .acciones .secundario { flex: 1; }
        .feedback-gracias { font-size: 0.78rem; color: #0f7b3f; margin: 0.4rem 0 0; }
        .demo-nota { font-size: 0.66rem; color: #9aa5b1; margin-top: 0.7rem; text-align: center; }
      </style>
      <div class="panel" role="dialog" aria-label="${titulo}">
        <div class="encabezado">
          <div class="titulo">${titulo}</div>
          <div class="subtitulo">Canal de orientacion - version demostrativa</div>
          <span class="conexion">Sin conexion</span>
        </div>
        <div class="cuerpo">
          <form class="inicio">
            <p class="intro">
              Bienvenido al canal de orientacion institucional. Cuentanos tu consulta y
              un funcionario te atendera. Es una demostracion: no envies datos sensibles.
            </p>
            <label for="nombre">Nombre o alias (opcional)</label>
            <input id="nombre" class="nombre" type="text" maxlength="80" autocomplete="off"
                   placeholder="Como quieres que te llamemos?" />
            <label for="topic">Tema de tu consulta</label>
            <select id="topic" class="topic">${opciones}</select>
            <label class="consent">
              <input type="checkbox" class="consent-check" />
              <span>
                Este canal es de orientacion institucional. No envies datos sensibles.
                La conversacion puede registrarse para trazabilidad. Al continuar aceptas
                el tratamiento de datos segun la politica institucional correspondiente.
              </span>
            </label>
            <button type="submit" class="primario inicio-boton">Iniciar chat</button>
          </form>

          <div class="chat">
            <div class="banner">Esperando a un funcionario...</div>
            <div class="mensajes" aria-live="polite"></div>
            <form class="envio">
              <input type="text" maxlength="2000" autocomplete="off"
                     placeholder="Escribe tu mensaje..." aria-label="Mensaje" />
              <button type="submit">Enviar</button>
            </form>
          </div>

          <div class="cierre">
            <p class="cierre-texto">${CLOSED_TEXT}</p>
            <form class="feedback">
              <label>Como calificarias la atencion?</label>
              <div class="stars">${estrellas}</div>
              <label for="comentario">Comentario (opcional)</label>
              <textarea id="comentario" class="feedback-comment" maxlength="500"
                        placeholder="Cuentanos tu experiencia..."></textarea>
              <button type="submit" class="primario">Enviar encuesta</button>
              <p class="feedback-gracias" hidden>Gracias por tu retroalimentacion.</p>
            </form>
            <div class="acciones">
              <button type="button" class="secundario copiar">Copiar transcripcion</button>
              <button type="button" class="secundario descargar">Descargar .txt</button>
            </div>
            <button type="button" class="primario nueva-conversacion" style="margin-top:0.6rem;">Nueva conversacion</button>
          </div>

          <p class="estado"></p>
          <p class="demo-nota">Prototipo demostrativo. No apto para produccion.</p>
        </div>
      </div>
      <button class="boton" type="button" aria-label="Abrir chat" aria-expanded="false">&#128172;</button>
    `;
  }

  #alternar(): void {
    this.#abierto = !this.#abierto;
    this.#panel.classList.toggle('abierto', this.#abierto);
    this.#boton.setAttribute('aria-expanded', String(this.#abierto));
  }

  #abrir(): void {
    this.#abierto = true;
    this.#panel.classList.add('abierto');
    this.#boton.setAttribute('aria-expanded', 'true');
  }

  #actualizarBotonInicio(): void {
    const boton = this.#vistaInicio.querySelector<HTMLButtonElement>('.inicio-boton')!;
    boton.disabled = !this.#consentCheck.checked;
  }

  #estadoConexionUi(estado: 'conectado' | 'conectando' | 'reconectando' | 'sin-conexion'): void {
    const etiquetas: Record<string, string> = {
      conectado: 'Conectado',
      conectando: 'Conectando...',
      reconectando: 'Reconectando...',
      'sin-conexion': 'Sin conexion',
    };
    this.#estadoConexion.className = `conexion ${estado}`;
    this.#estadoConexion.textContent = etiquetas[estado];
  }

  async #conectar(): Promise<HubConnection> {
    if (this.#conexion) {
      return this.#conexion;
    }
    this.#estadoConexionUi('conectando');
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
    conexion.on('ConversationClosed', (conversation: ConversationDto) => {
      if (conversation.id === this.#conversacion?.id) {
        this.#conversacion = conversation;
        this.#aplicarEstadoConversacion();
      }
    });
    conexion.onreconnecting(() => this.#estadoConexionUi('reconectando'));
    conexion.onreconnected(() => {
      this.#estadoConexionUi('conectado');
      void this.#repintarHistorial();
    });
    conexion.onclose(() => this.#estadoConexionUi('sin-conexion'));

    await conexion.start();
    this.#estadoConexionUi('conectado');
    this.#conexion = conexion;
    return conexion;
  }

  async #reanudarConversacion(): Promise<void> {
    const conversationId = sessionStorage.getItem(STORAGE_KEY);
    if (!conversationId) {
      return;
    }
    this.#estado('Recuperando conversacion...');
    try {
      const conexion = await this.#conectar();
      const [historial, conversation] = await Promise.all([
        conexion.invoke<ChatMessageDto[]>('JoinConversation', conversationId),
        conexion.invoke<ConversationDto>('GetConversation', conversationId),
      ]);
      this.#conversacion = conversation;
      this.#mostrarChat();
      this.#reiniciarTimeline();
      historial.forEach((m) => this.#pintarMensaje(m));
      this.#aplicarEstadoConversacion();
      this.#abrir();
      this.#estado('');
    } catch {
      sessionStorage.removeItem(STORAGE_KEY);
      this.#reiniciarConversacion(false);
      this.#estado('No fue posible recuperar la conversacion anterior. Puedes iniciar una nueva.', true);
    }
  }

  async #iniciarConversacion(): Promise<void> {
    if (!this.#consentCheck.checked) {
      this.#estado('Debes aceptar el aviso de tratamiento de datos para iniciar.', true);
      return;
    }
    const boton = this.#vistaInicio.querySelector<HTMLButtonElement>('.inicio-boton')!;
    boton.disabled = true;
    this.#estado('Conectando...');
    try {
      const conexion = await this.#conectar();
      const nombre = this.#vistaInicio.querySelector<HTMLInputElement>('.nombre')!.value.trim() || null;
      const topic = this.#topicSelect.value;
      this.#conversacion = await conexion.invoke<ConversationDto>('StartConversation', {
        displayName: nombre,
        topic,
        consentAccepted: true,
        consentVersion: CONSENT_VERSION,
      });
      sessionStorage.setItem(STORAGE_KEY, this.#conversacion.id);
      this.#reiniciarTimeline();
      this.#mostrarChat();
      this.#aplicarEstadoConversacion();
      this.#campoMensaje.focus();
      this.#estado('');
    } catch (error) {
      if (String(error).includes('Consent is required')) {
        this.#estado('Debes aceptar el aviso de tratamiento de datos para iniciar.', true);
      } else {
        this.#estado('No fue posible conectar con el chat. Intenta de nuevo.', true);
      }
    } finally {
      boton.disabled = !this.#consentCheck.checked;
    }
  }

  async #enviarMensaje(): Promise<void> {
    const contenido = this.#campoMensaje.value.trim();
    if (!contenido || !this.#conexion || !this.#conversacion || this.#conversacion.status === 'Closed') {
      return;
    }
    try {
      await this.#conexion.invoke('SendVisitorMessage', {
        conversationId: this.#conversacion.id,
        content: contenido,
      });
      this.#campoMensaje.value = '';
      this.#estado('');
    } catch (error) {
      if (String(error).includes('Conversation is closed')) {
        this.#conversacion = { ...this.#conversacion, status: 'Closed', closedAtUtc: new Date().toISOString() };
        this.#aplicarEstadoConversacion();
        return;
      }
      this.#estado('El mensaje no pudo enviarse.', true);
    }
  }

  async #repintarHistorial(): Promise<void> {
    if (!this.#conexion || !this.#conversacion) {
      return;
    }
    const [historial, conversation] = await Promise.all([
      this.#conexion.invoke<ChatMessageDto[]>('JoinConversation', this.#conversacion.id),
      this.#conexion.invoke<ConversationDto>('GetConversation', this.#conversacion.id),
    ]);
    this.#conversacion = conversation;
    this.#reiniciarTimeline();
    historial.forEach((m) => this.#pintarMensaje(m));
    this.#aplicarEstadoConversacion();
  }

  #mostrarChat(): void {
    this.#vistaInicio.style.display = 'none';
    this.#vistaCierre.classList.remove('activo');
    this.#vistaChat.classList.add('activo');
  }

  #reiniciarTimeline(): void {
    this.#mensajesPintados.clear();
    this.#transcripcion = [];
    this.#listaMensajes.replaceChildren();
  }

  #reiniciarConversacion(limpiarEstado = true): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.#conversacion = null;
    this.#feedbackEnviado = false;
    this.#ratingSeleccionado = 0;
    this.#reiniciarTimeline();
    this.#vistaChat.classList.remove('activo');
    this.#vistaCierre.classList.remove('activo');
    this.#vistaInicio.style.display = 'block';
    this.#campoMensaje.value = '';
    this.#campoMensaje.disabled = false;
    this.#botonEnviar.disabled = false;
    this.#consentCheck.checked = false;
    this.#comentarioFeedback.value = '';
    this.#graciasFeedback.hidden = true;
    this.#formularioFeedback.querySelector('button')!.disabled = false;
    this.#actualizarEstrellas();
    this.#actualizarBotonInicio();
    if (limpiarEstado) {
      this.#estado('');
    }
  }

  #aplicarEstadoConversacion(): void {
    const status = this.#conversacion?.status;
    if (status === 'Closed') {
      this.#vistaChat.classList.remove('activo');
      this.#vistaCierre.classList.add('activo');
      this.#estado('');
      return;
    }

    const tieneRespuestaAgente = this.#transcripcion.some((m) => m.senderType === 'Agent');
    const activa = status === 'Active' || tieneRespuestaAgente;
    this.#bannerEstado.classList.remove('oculto');
    this.#bannerEstado.classList.toggle('activa', activa);
    this.#bannerEstado.textContent = activa
      ? 'Un funcionario esta atendiendo tu consulta.'
      : 'Esperando a un funcionario...';
    this.#campoMensaje.disabled = false;
    this.#botonEnviar.disabled = false;
  }

  #pintarMensaje(mensaje: ChatMessageDto): void {
    if (this.#mensajesPintados.has(mensaje.id)) {
      return;
    }
    this.#mensajesPintados.add(mensaje.id);
    this.#transcripcion.push(mensaje);

    const fila = document.createElement('div');
    fila.className = `fila ${mensaje.senderType.toLowerCase()}`;
    const remitente = document.createElement('span');
    remitente.className = 'remitente';
    remitente.textContent = mensaje.senderType === 'Agent' ? 'Funcionario' : 'Tu';
    const burbuja = document.createElement('div');
    burbuja.className = 'mensaje';
    // textContent (nunca innerHTML): el contenido es entrada no confiable.
    burbuja.textContent = mensaje.content;
    fila.append(remitente, burbuja);
    this.#listaMensajes.appendChild(fila);
    this.#listaMensajes.scrollTop = this.#listaMensajes.scrollHeight;

    if (this.#conversacion && this.#conversacion.status !== 'Closed' && mensaje.senderType === 'Agent') {
      this.#aplicarEstadoConversacion();
    }
  }

  #seleccionarRating(valor: number): void {
    if (this.#feedbackEnviado) {
      return;
    }
    this.#ratingSeleccionado = valor;
    this.#actualizarEstrellas();
  }

  #actualizarEstrellas(): void {
    this.#formularioFeedback.querySelectorAll<HTMLButtonElement>('.star').forEach((star) => {
      star.classList.toggle('activa', Number(star.dataset.value) <= this.#ratingSeleccionado);
    });
  }

  async #enviarFeedback(): Promise<void> {
    if (this.#feedbackEnviado || !this.#conversacion) {
      return;
    }
    if (this.#ratingSeleccionado < 1) {
      this.#estado('Selecciona una calificacion de 1 a 5.', true);
      return;
    }
    const boton = this.#formularioFeedback.querySelector('button')!;
    boton.disabled = true;
    try {
      const respuesta = await fetch(
        `${this.#apiBase}/api/conversations/${this.#conversacion.id}/feedback`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            rating: this.#ratingSeleccionado,
            comment: this.#comentarioFeedback.value.trim() || null,
          }),
        },
      );
      if (!respuesta.ok) {
        boton.disabled = false;
        this.#estado('No fue posible registrar la encuesta.', true);
        return;
      }
      this.#feedbackEnviado = true;
      this.#graciasFeedback.hidden = false;
      this.#comentarioFeedback.disabled = true;
      this.#estado('');
    } catch {
      boton.disabled = false;
      this.#estado('No fue posible registrar la encuesta.', true);
    }
  }

  #transcripcionTexto(): string {
    const fecha = new Date().toLocaleString();
    const ciudadano = this.#conversacion?.visitorDisplayName ?? 'Anonimo';
    const estado = this.#conversacion?.status ?? 'Desconocido';
    const lineas = this.#transcripcion.map((m) => {
      const quien = m.senderType === 'Agent' ? 'Funcionario' : 'Ciudadano';
      return `[${quien}] ${m.content}`;
    });
    return [
      'Chat institucional demo',
      `Fecha: ${fecha}`,
      `Ciudadano: ${ciudadano}`,
      `Estado: ${estado}`,
      'Mensajes visibles:',
      ...lineas,
    ].join('\n');
  }

  async #copiarTranscripcion(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.#transcripcionTexto());
      this.#estado('Transcripcion copiada.');
    } catch {
      this.#estado('No fue posible copiar. Usa la opcion de descarga.', true);
    }
  }

  #descargarTranscripcion(): void {
    const blob = new Blob([this.#transcripcionTexto()], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = 'chat-institucional-demo.txt';
    enlace.click();
    URL.revokeObjectURL(url);
  }

  #estado(texto: string, esError = false): void {
    this.#lineaEstado.textContent = texto;
    this.#lineaEstado.classList.toggle('error', esError);
  }
}

if (!customElements.get(AndjeChatWidget.tagName)) {
  customElements.define(AndjeChatWidget.tagName, AndjeChatWidget);
}

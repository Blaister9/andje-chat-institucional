import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';
import { FormEvent, useEffect, useRef, useState } from 'react';
import { ChatMessageDto, ConversationDto } from './types';

const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:8080';

type ConnectionStatus = 'conectando' | 'conectado' | 'desconectado';

function appendUnique(
  byConversation: Record<string, ChatMessageDto[]>,
  message: ChatMessageDto,
): Record<string, ChatMessageDto[]> {
  const current = byConversation[message.conversationId] ?? [];
  if (current.some((m) => m.id === message.id)) {
    return byConversation;
  }
  return { ...byConversation, [message.conversationId]: [...current, message] };
}

function statusLabel(status: ConversationDto['status']): string {
  if (status === 'Pending') {
    return 'En espera';
  }
  if (status === 'Active') {
    return 'Activa';
  }
  return 'Cerrada';
}

export function App() {
  const [status, setStatus] = useState<ConnectionStatus>('conectando');
  const [conversations, setConversations] = useState<ConversationDto[]>([]);
  const [messagesByConversation, setMessagesByConversation] = useState<
    Record<string, ChatMessageDto[]>
  >({});
  const [unread, setUnread] = useState<Record<string, number>>({});
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const [sendError, setSendError] = useState('');
  const [showClosed, setShowClosed] = useState(false);

  const connectionRef = useRef<HubConnection | null>(null);
  const selectedIdRef = useRef<string | null>(null);
  selectedIdRef.current = selectedId;
  const messagesEndRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/chat`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = connection;

    connection.on('ConversationStarted', (conversation: ConversationDto) => {
      setConversations((prev) =>
        prev.some((c) => c.id === conversation.id) ? prev : [...prev, conversation],
      );
    });

    const updateConversation = (conversation: ConversationDto) => {
      setConversations((prev) =>
        prev.map((c) => (c.id === conversation.id ? conversation : c)),
      );
    };

    connection.on('ConversationUpdated', updateConversation);

    connection.on('ConversationClosed', (conversation: ConversationDto) => {
      updateConversation(conversation);
      if (selectedIdRef.current === conversation.id) {
        setSendError('La conversacion fue cerrada.');
      }
    });

    connection.on('MessageReceived', (message: ChatMessageDto) => {
      setMessagesByConversation((prev) => appendUnique(prev, message));
      if (
        message.senderType === 'Visitor' &&
        selectedIdRef.current !== message.conversationId
      ) {
        setUnread((prev) => ({
          ...prev,
          [message.conversationId]: (prev[message.conversationId] ?? 0) + 1,
        }));
      }
    });

    const joinConsole = () =>
      connection
        .invoke<ConversationDto[]>('JoinAgentConsole')
        .then((list) => {
          setConversations(list);
          setStatus('conectado');
        });

    connection.onreconnecting(() => setStatus('desconectado'));
    connection.onreconnected(() => {
      void joinConsole().catch(() => setStatus('desconectado'));
    });

    connection
      .start()
      .then(joinConsole)
      .catch(() => setStatus('desconectado'));

    return () => {
      connectionRef.current = null;
      void connection.stop();
    };
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ block: 'end' });
  }, [messagesByConversation, selectedId]);

  async function selectConversation(id: string) {
    setSelectedId(id);
    setUnread((prev) => ({ ...prev, [id]: 0 }));
    setSendError('');
    const connection = connectionRef.current;
    if (!connection) {
      return;
    }
    try {
      const history = await connection.invoke<ChatMessageDto[]>(
        'GetConversationHistory',
        id,
      );
      setMessagesByConversation((prev) => {
        const merged = [...history];
        for (const message of prev[id] ?? []) {
          if (!merged.some((m) => m.id === message.id)) {
            merged.push(message);
          }
        }
        return { ...prev, [id]: merged };
      });
    } catch {
      setSendError('No fue posible cargar el historial.');
    }
  }

  async function sendReply(event: FormEvent) {
    event.preventDefault();
    const content = draft.trim();
    const connection = connectionRef.current;
    const selected = conversations.find((c) => c.id === selectedId) ?? null;
    if (!content || !connection || !selected || selected.status === 'Closed') {
      return;
    }
    try {
      await connection.invoke('SendAgentMessage', {
        conversationId: selected.id,
        content,
      });
      setDraft('');
      setSendError('');
    } catch (error) {
      if (String(error).includes('Conversation is closed')) {
        setConversations((prev) =>
          prev.map((conversation) =>
            conversation.id === selected.id
              ? { ...conversation, status: 'Closed', closedAtUtc: new Date().toISOString() }
              : conversation,
          ),
        );
        setSendError('La conversacion ya fue cerrada.');
        return;
      }
      setSendError('La respuesta no pudo enviarse.');
    }
  }

  async function closeSelectedConversation() {
    const connection = connectionRef.current;
    const selected = conversations.find((c) => c.id === selectedId) ?? null;
    if (!connection || !selected || selected.status === 'Closed') {
      return;
    }
    if (!confirm('Cerrar esta conversacion?')) {
      return;
    }
    try {
      const closed = await connection.invoke<ConversationDto>(
        'CloseConversation',
        selected.id,
      );
      setConversations((prev) =>
        prev.map((conversation) =>
          conversation.id === closed.id ? closed : conversation,
        ),
      );
      setDraft('');
      setSendError('La conversacion fue cerrada.');
    } catch {
      setSendError('No fue posible cerrar la conversacion.');
    }
  }

  const selected = conversations.find((c) => c.id === selectedId) ?? null;
  const isSelectedClosed = selected?.status === 'Closed';
  const selectedMessages = selectedId
    ? (messagesByConversation[selectedId] ?? [])
    : [];
  const visibleConversations = showClosed
    ? conversations
    : conversations.filter((conversation) => conversation.status !== 'Closed');

  return (
    <div className="layout">
      <header className="header">
        <h1>Consola de agentes</h1>
        <span className="subtitle">Chat institucional - ANDJE</span>
        <span className={`estado-conexion ${status}`}>{status}</span>
      </header>

      <main className="panels">
        <section className="panel" aria-label="Cola de conversaciones">
          <div className="panel-heading">
            <h2>Cola de conversaciones</h2>
            <label className="toggle-cerradas">
              <input
                type="checkbox"
                checked={showClosed}
                onChange={(event) => setShowClosed(event.target.checked)}
              />
              Ver cerradas
            </label>
          </div>
          {visibleConversations.length === 0 ? (
            <p className="placeholder">
              Sin conversaciones. Inicie una desde el widget demo
              (http://localhost:5174).
            </p>
          ) : (
            <ul className="cola">
              {visibleConversations.map((conversation) => (
                <li key={conversation.id}>
                  <button
                    type="button"
                    className={`item-cola ${conversation.id === selectedId ? 'seleccionada' : ''}`}
                    onClick={() => void selectConversation(conversation.id)}
                  >
                    <span className="nombre">
                      {conversation.visitorDisplayName ?? 'Ciudadano anonimo'}
                    </span>
                    <span className={`badge ${conversation.status.toLowerCase()}`}>
                      {statusLabel(conversation.status)}
                    </span>
                    {(unread[conversation.id] ?? 0) > 0 && (
                      <span className="badge no-leidos">{unread[conversation.id]}</span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="panel" aria-label="Conversacion activa">
          <h2>
            {selected
              ? `Conversacion con ${selected.visitorDisplayName ?? 'ciudadano anonimo'}`
              : 'Conversacion activa'}
          </h2>
          {selected ? (
            <>
              <div className="mensajes">
                {selectedMessages.map((message) => (
                  <div
                    key={message.id}
                    className={`mensaje ${message.senderType.toLowerCase()}`}
                  >
                    {message.content}
                  </div>
                ))}
                <div ref={messagesEndRef} />
              </div>
              <div className="acciones-conversacion">
                <span className={`badge ${selected.status.toLowerCase()}`}>
                  {statusLabel(selected.status)}
                </span>
                {!isSelectedClosed && (
                  <button
                    type="button"
                    className="boton-secundario"
                    onClick={() => void closeSelectedConversation()}
                  >
                    Cerrar conversacion
                  </button>
                )}
              </div>
              <form className="envio" onSubmit={(e) => void sendReply(e)}>
                <input
                  type="text"
                  value={draft}
                  maxLength={2000}
                  autoComplete="off"
                  placeholder={isSelectedClosed ? 'Conversacion cerrada' : 'Escriba la respuesta...'}
                  aria-label="Respuesta"
                  disabled={isSelectedClosed}
                  onChange={(e) => setDraft(e.target.value)}
                />
                <button type="submit" disabled={isSelectedClosed}>Responder</button>
              </form>
              {isSelectedClosed && (
                <p className="placeholder">
                  Esta conversacion esta cerrada. No se pueden enviar nuevas respuestas.
                </p>
              )}
              {sendError && <p className="error">{sendError}</p>}
            </>
          ) : (
            <p className="placeholder">
              Seleccione una conversacion de la cola para atenderla.
            </p>
          )}
        </section>
      </main>

      <footer className="footer">
        Prototipo interno - fase 03: ciclo de vida minimo de conversacion.
        No apto para produccion.
      </footer>
    </div>
  );
}

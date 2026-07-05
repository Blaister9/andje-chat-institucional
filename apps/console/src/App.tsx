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

    connection.on('ConversationUpdated', (conversation: ConversationDto) => {
      setConversations((prev) =>
        prev.map((c) => (c.id === conversation.id ? conversation : c)),
      );
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
    if (!content || !connection || !selectedId) {
      return;
    }
    try {
      // La respuesta se pinta cuando el servidor la confirma (eco del grupo).
      await connection.invoke('SendAgentMessage', {
        conversationId: selectedId,
        content,
      });
      setDraft('');
      setSendError('');
    } catch {
      setSendError('La respuesta no pudo enviarse.');
    }
  }

  const selected = conversations.find((c) => c.id === selectedId) ?? null;
  const selectedMessages = selectedId
    ? (messagesByConversation[selectedId] ?? [])
    : [];

  return (
    <div className="layout">
      <header className="header">
        <h1>Consola de agentes</h1>
        <span className="subtitle">Chat institucional — ANDJE</span>
        <span className={`estado-conexion ${status}`}>{status}</span>
      </header>

      <main className="panels">
        <section className="panel" aria-label="Cola de conversaciones">
          <h2>Cola de conversaciones</h2>
          {conversations.length === 0 ? (
            <p className="placeholder">
              Sin conversaciones. Inicie una desde el widget demo
              (http://localhost:5174).
            </p>
          ) : (
            <ul className="cola">
              {conversations.map((conversation) => (
                <li key={conversation.id}>
                  <button
                    type="button"
                    className={`item-cola ${conversation.id === selectedId ? 'seleccionada' : ''}`}
                    onClick={() => void selectConversation(conversation.id)}
                  >
                    <span className="nombre">
                      {conversation.visitorDisplayName ?? 'Ciudadano anónimo'}
                    </span>
                    <span className={`badge ${conversation.status.toLowerCase()}`}>
                      {conversation.status === 'Pending' ? 'En espera' : 'Activa'}
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

        <section className="panel" aria-label="Conversación activa">
          <h2>
            {selected
              ? `Conversación con ${selected.visitorDisplayName ?? 'ciudadano anónimo'}`
              : 'Conversación activa'}
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
              <form className="envio" onSubmit={(e) => void sendReply(e)}>
                <input
                  type="text"
                  value={draft}
                  maxLength={2000}
                  autoComplete="off"
                  placeholder="Escriba la respuesta…"
                  aria-label="Respuesta"
                  onChange={(e) => setDraft(e.target.value)}
                />
                <button type="submit">Responder</button>
              </form>
              {sendError && <p className="error">{sendError}</p>}
            </>
          ) : (
            <p className="placeholder">
              Seleccione una conversación de la cola para atenderla.
            </p>
          )}
        </section>
      </main>

      <footer className="footer">
        Prototipo interno — fase 01: mensajería en tiempo real sin persistencia.
        No apto para producción.
      </footer>
    </div>
  );
}

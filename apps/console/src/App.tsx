import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';
import { FormEvent, useEffect, useRef, useState } from 'react';
import {
  API_BASE,
  assignTag,
  createCannedResponse,
  createInternalNote,
  deactivateCannedResponse,
  getCannedResponses,
  getConsoleConversations,
  getInternalNotes,
  getSummary,
  getTags,
  removeTag,
  SessionExpiredError,
  updateCannedResponse,
} from './api';
import {
  DashboardCards,
  ConversationDetail,
  ConversationQueue,
  LoginView,
  SettingsPanel,
} from './components/ConsoleViews';
import { ConnectionStatus } from './components/ConsoleViews';
import {
  AgentSessionDto,
  CannedResponseDto,
  ChatMessageDto,
  ConsoleConversationDto,
  ConsoleSummaryDto,
  ConversationDto,
  ConversationFilter,
  ConversationTagDto,
  InternalNoteDto,
} from './types';

const TOKEN_KEY = 'andje-chat.agent.accessToken';
const DISPLAY_NAME_KEY = 'andje-chat.agent.displayName';
const EXPIRES_AT_KEY = 'andje-chat.agent.expiresAtUtc';

type AgentSession = {
  accessToken: string;
  agentDisplayName: string;
  expiresAtUtc: string;
};

type ConsoleSection = 'attention' | 'settings';

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

function readStoredSession(): AgentSession | null {
  const accessToken = sessionStorage.getItem(TOKEN_KEY);
  const agentDisplayName = sessionStorage.getItem(DISPLAY_NAME_KEY);
  const expiresAtUtc = sessionStorage.getItem(EXPIRES_AT_KEY);
  if (!accessToken || !agentDisplayName || !expiresAtUtc) {
    clearStoredSession();
    return null;
  }

  if (Date.parse(expiresAtUtc) <= Date.now()) {
    clearStoredSession();
    return null;
  }

  return { accessToken, agentDisplayName, expiresAtUtc };
}

function storeSession(session: AgentSession): void {
  sessionStorage.setItem(TOKEN_KEY, session.accessToken);
  sessionStorage.setItem(DISPLAY_NAME_KEY, session.agentDisplayName);
  sessionStorage.setItem(EXPIRES_AT_KEY, session.expiresAtUtc);
}

function clearStoredSession(): void {
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(DISPLAY_NAME_KEY);
  sessionStorage.removeItem(EXPIRES_AT_KEY);
}

function toConsoleConversation(
  conversation: ConversationDto,
  previous?: ConsoleConversationDto,
): ConsoleConversationDto {
  return {
    ...conversation,
    topic: conversation.topic ?? previous?.topic ?? null,
    feedbackRating: previous?.feedbackRating ?? null,
    lastMessagePreview: previous?.lastMessagePreview ?? null,
    lastMessageAtUtc: previous?.lastMessageAtUtc ?? conversation.updatedAtUtc,
    tags: previous?.tags ?? [],
  };
}

function preview(content: string): string {
  return content.length <= 140 ? content : `${content.slice(0, 140)}...`;
}

function isSessionError(error: unknown): boolean {
  return error instanceof SessionExpiredError || String(error).includes('Agent session');
}

export function App() {
  const [agentSession, setAgentSession] = useState<AgentSession | null>(() => readStoredSession());
  const [status, setStatus] = useState<ConnectionStatus>(
    agentSession ? 'conectando' : 'desconectado',
  );
  const [section, setSection] = useState<ConsoleSection>('attention');
  const [summary, setSummary] = useState<ConsoleSummaryDto | null>(null);
  const [conversations, setConversations] = useState<ConsoleConversationDto[]>([]);
  const [messagesByConversation, setMessagesByConversation] = useState<
    Record<string, ChatMessageDto[]>
  >({});
  const [notesByConversation, setNotesByConversation] = useState<
    Record<string, InternalNoteDto[]>
  >({});
  const [unread, setUnread] = useState<Record<string, number>>({});
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const [noteDraft, setNoteDraft] = useState('');
  const [sendError, setSendError] = useState('');
  const [noteError, setNoteError] = useState('');
  const [globalError, setGlobalError] = useState('');
  const [loadingConsole, setLoadingConsole] = useState(false);
  const [filter, setFilter] = useState<ConversationFilter>('open');
  const [search, setSearch] = useState('');
  const [cannedResponses, setCannedResponses] = useState<CannedResponseDto[]>([]);
  const [tags, setTags] = useState<ConversationTagDto[]>([]);
  const [editingResponse, setEditingResponse] = useState<CannedResponseDto | null>(null);
  const [responseTitle, setResponseTitle] = useState('');
  const [responseBody, setResponseBody] = useState('');
  const [responseSortOrder, setResponseSortOrder] = useState(100);
  const [settingsError, setSettingsError] = useState('');
  const [loginName, setLoginName] = useState('');
  const [loginCode, setLoginCode] = useState('');
  const [loginError, setLoginError] = useState('');
  const [loginSubmitting, setLoginSubmitting] = useState(false);

  const connectionRef = useRef<HubConnection | null>(null);
  const selectedIdRef = useRef<string | null>(null);
  selectedIdRef.current = selectedId;

  useEffect(() => {
    if (!agentSession) {
      setStatus('desconectado');
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/chat`, {
        accessTokenFactory: () => agentSession.accessToken,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = connection;

    connection.on('ConversationStarted', (conversation: ConversationDto) => {
      setConversations((prev) => upsertConversation(prev, conversation));
      void refreshConsoleData(agentSession.accessToken, false);
    });

    const updateConversation = (conversation: ConversationDto) => {
      setConversations((prev) => upsertConversation(prev, conversation));
    };

    connection.on('ConversationUpdated', updateConversation);

    connection.on('ConversationClosed', (conversation: ConversationDto) => {
      updateConversation(conversation);
      if (selectedIdRef.current === conversation.id) {
        setSendError('La conversacion fue cerrada.');
      }
      void refreshConsoleData(agentSession.accessToken, false);
    });

    connection.on('MessageReceived', (message: ChatMessageDto) => {
      setMessagesByConversation((prev) => appendUnique(prev, message));
      setConversations((prev) =>
        prev.map((conversation) =>
          conversation.id === message.conversationId
            ? {
                ...conversation,
                lastMessagePreview: preview(message.content),
                lastMessageAtUtc: message.sentAt,
                updatedAtUtc: message.sentAt,
              }
            : conversation,
        ),
      );

      if (
        message.senderType === 'Visitor' &&
        selectedIdRef.current !== message.conversationId
      ) {
        setUnread((prev) => ({
          ...prev,
          [message.conversationId]: (prev[message.conversationId] ?? 0) + 1,
        }));
      }
      void refreshSummary(agentSession.accessToken);
    });

    const joinConsole = () =>
      connection
        .invoke<ConversationDto[]>('JoinAgentConsole')
        .then((list) => {
          setConversations((prev) =>
            list.map((conversation) =>
              toConsoleConversation(
                conversation,
                prev.find((existing) => existing.id === conversation.id),
              ),
            ),
          );
          setStatus('conectado');
          setLoginError('');
          void refreshConsoleData(agentSession.accessToken);
        })
        .catch((error) => handleSessionFailure(error));

    connection.onreconnecting(() => setStatus('reconectando'));
    connection.onreconnected(() => {
      setStatus('conectando');
      void joinConsole();
    });
    connection.onclose(() => setStatus('desconectado'));

    connection
      .start()
      .then(joinConsole)
      .catch((error) => handleSessionFailure(error));

    return () => {
      connectionRef.current = null;
      void connection.stop();
    };
  }, [agentSession]);

  async function submitLogin(event: FormEvent) {
    event.preventDefault();
    setLoginSubmitting(true);
    setLoginError('');
    try {
      const response = await fetch(`${API_BASE}/api/agent-sessions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          displayName: loginName,
          accessCode: loginCode,
        }),
      });

      if (response.status === 429) {
        setLoginError('Demasiados intentos. Espere un momento e intente de nuevo.');
        return;
      }

      if (!response.ok) {
        setLoginError('Codigo de acceso invalido.');
        return;
      }

      const session = (await response.json()) as AgentSessionDto;
      const nextSession = {
        accessToken: session.accessToken,
        agentDisplayName: session.agentDisplayName,
        expiresAtUtc: session.expiresAtUtc,
      };
      storeSession(nextSession);
      setAgentSession(nextSession);
      setLoginCode('');
    } catch {
      setLoginError('No fue posible iniciar sesion de agente.');
    } finally {
      setLoginSubmitting(false);
    }
  }

  function logout() {
    clearStoredSession();
    setAgentSession(null);
    setSummary(null);
    setConversations([]);
    setMessagesByConversation({});
    setNotesByConversation({});
    setUnread({});
    setSelectedId(null);
    setDraft('');
    setNoteDraft('');
    setSendError('');
    setNoteError('');
    setGlobalError('');
    setLoginError('');
    setStatus('desconectado');
    void connectionRef.current?.stop();
  }

  function handleSessionFailure(error: unknown) {
    if (isSessionError(error)) {
      logout();
      setLoginError('La sesion de agente expiro o no es valida.');
      return;
    }
    setStatus('desconectado');
    setGlobalError('No fue posible conectar con el backend.');
  }

  async function refreshSummary(accessToken = agentSession?.accessToken) {
    if (!accessToken) {
      return;
    }
    try {
      setSummary(await getSummary(accessToken));
    } catch (error) {
      if (isSessionError(error)) {
        handleSessionFailure(error);
      }
    }
  }

  async function refreshConsoleData(
    accessToken = agentSession?.accessToken,
    showLoading = true,
  ) {
    if (!accessToken) {
      return;
    }
    if (showLoading) {
      setLoadingConsole(true);
    }
    try {
      const [nextSummary, nextConversations, nextResponses, nextTags] = await Promise.all([
        getSummary(accessToken),
        getConsoleConversations(accessToken),
        getCannedResponses(accessToken),
        getTags(accessToken),
      ]);
      setSummary(nextSummary);
      setConversations(nextConversations);
      setCannedResponses(nextResponses);
      setTags(nextTags);
      setGlobalError('');
      setSelectedId((current) =>
        current && nextConversations.some((conversation) => conversation.id === current)
          ? current
          : current,
      );
    } catch (error) {
      if (isSessionError(error)) {
        handleSessionFailure(error);
      } else {
        setGlobalError('No fue posible cargar datos de consola.');
      }
    } finally {
      setLoadingConsole(false);
    }
  }

  async function selectConversation(id: string) {
    setSelectedId(id);
    setUnread((prev) => ({ ...prev, [id]: 0 }));
    setSendError('');
    setNoteError('');
    const connection = connectionRef.current;
    if (!connection || !agentSession) {
      return;
    }
    try {
      const [history, notes] = await Promise.all([
        connection.invoke<ChatMessageDto[]>('GetConversationHistory', id),
        getInternalNotes(agentSession.accessToken, id),
      ]);
      setMessagesByConversation((prev) => {
        const merged = [...history];
        for (const message of prev[id] ?? []) {
          if (!merged.some((item) => item.id === message.id)) {
            merged.push(message);
          }
        }
        return { ...prev, [id]: merged };
      });
      setNotesByConversation((prev) => ({ ...prev, [id]: notes }));
    } catch (error) {
      if (isSessionError(error)) {
        handleSessionFailure(error);
        return;
      }
      setSendError('No fue posible cargar el historial.');
    }
  }

  async function sendReply(event: FormEvent) {
    event.preventDefault();
    const content = draft.trim();
    const connection = connectionRef.current;
    const selected = conversations.find((conversation) => conversation.id === selectedId) ?? null;
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
      if (isSessionError(error)) {
        handleSessionFailure(error);
        return;
      }
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
    const selected = conversations.find((conversation) => conversation.id === selectedId) ?? null;
    if (!connection || !selected || selected.status === 'Closed') {
      return;
    }
    if (!confirm('Cerrar esta conversacion?')) {
      return;
    }
    try {
      const closed = await connection.invoke<ConversationDto>('CloseConversation', selected.id);
      setConversations((prev) =>
        prev.map((conversation) =>
          conversation.id === closed.id ? toConsoleConversation(closed, conversation) : conversation,
        ),
      );
      setDraft('');
      setSendError('La conversacion fue cerrada.');
      await refreshConsoleData(agentSession?.accessToken, false);
    } catch (error) {
      if (isSessionError(error)) {
        handleSessionFailure(error);
        return;
      }
      setSendError('No fue posible cerrar la conversacion.');
    }
  }

  async function addNote(event: FormEvent) {
    event.preventDefault();
    if (!agentSession || !selectedId) {
      return;
    }
    const body = noteDraft.trim();
    if (!body) {
      return;
    }
    try {
      const note = await createInternalNote(agentSession.accessToken, selectedId, body);
      setNotesByConversation((prev) => ({
        ...prev,
        [selectedId]: [...(prev[selectedId] ?? []), note],
      }));
      setNoteDraft('');
      setNoteError('');
    } catch (error) {
      if (isSessionError(error)) {
        handleSessionFailure(error);
        return;
      }
      setNoteError('No fue posible guardar la nota interna.');
    }
  }

  async function assignConversationTag(tagId: string) {
    if (!agentSession || !selectedId) {
      return;
    }
    try {
      await assignTag(agentSession.accessToken, selectedId, tagId);
      await refreshConsoleData(agentSession.accessToken, false);
    } catch (error) {
      if (isSessionError(error)) {
        handleSessionFailure(error);
        return;
      }
      setGlobalError('No fue posible asignar la etiqueta.');
    }
  }

  async function removeConversationTag(tagId: string) {
    if (!agentSession || !selectedId) {
      return;
    }
    try {
      await removeTag(agentSession.accessToken, selectedId, tagId);
      await refreshConsoleData(agentSession.accessToken, false);
    } catch (error) {
      if (isSessionError(error)) {
        handleSessionFailure(error);
        return;
      }
      setGlobalError('No fue posible quitar la etiqueta.');
    }
  }

  function insertQuickResponse(body: string) {
    setDraft((current) => (current.trim() ? `${current.trim()}\n\n${body}` : body));
  }

  function editResponse(response: CannedResponseDto) {
    setEditingResponse(response);
    setResponseTitle(response.title);
    setResponseBody(response.body);
    setResponseSortOrder(response.sortOrder);
    setSettingsError('');
  }

  function cancelEditResponse() {
    setEditingResponse(null);
    setResponseTitle('');
    setResponseBody('');
    setResponseSortOrder(100);
    setSettingsError('');
  }

  async function saveResponse(event: FormEvent) {
    event.preventDefault();
    if (!agentSession) {
      return;
    }
    const title = responseTitle.trim();
    const body = responseBody.trim();
    if (!title || !body) {
      setSettingsError('Titulo y cuerpo son requeridos.');
      return;
    }
    try {
      if (editingResponse) {
        await updateCannedResponse(
          agentSession.accessToken,
          editingResponse,
          title,
          body,
          responseSortOrder,
        );
      } else {
        await createCannedResponse(
          agentSession.accessToken,
          title,
          body,
          responseSortOrder,
        );
      }
      cancelEditResponse();
      setCannedResponses(await getCannedResponses(agentSession.accessToken));
    } catch (error) {
      if (isSessionError(error)) {
        handleSessionFailure(error);
        return;
      }
      setSettingsError('No fue posible guardar la respuesta rapida.');
    }
  }

  async function deactivateResponse(responseId: string) {
    if (!agentSession) {
      return;
    }
    try {
      await deactivateCannedResponse(agentSession.accessToken, responseId);
      setCannedResponses(await getCannedResponses(agentSession.accessToken));
    } catch (error) {
      if (isSessionError(error)) {
        handleSessionFailure(error);
        return;
      }
      setSettingsError('No fue posible desactivar la respuesta rapida.');
    }
  }

  if (!agentSession) {
    return (
      <LoginView
        loginName={loginName}
        loginCode={loginCode}
        loginError={loginError}
        loginSubmitting={loginSubmitting}
        onNameChange={setLoginName}
        onCodeChange={setLoginCode}
        onSubmit={(event) => void submitLogin(event)}
      />
    );
  }

  const selected = conversations.find((conversation) => conversation.id === selectedId) ?? null;
  const selectedMessages = selectedId ? (messagesByConversation[selectedId] ?? []) : [];
  const selectedNotes = selectedId ? (notesByConversation[selectedId] ?? []) : [];
  const filteredConversations = conversations
    .filter((conversation) => matchesFilter(conversation, filter))
    .filter((conversation) =>
      matchesSearch(conversation, search, messagesByConversation[conversation.id] ?? []),
    )
    .sort((left, right) =>
      Date.parse(right.lastMessageAtUtc ?? right.updatedAtUtc) -
      Date.parse(left.lastMessageAtUtc ?? left.updatedAtUtc),
    );

  return (
    <div className="console-shell">
      <aside className="sidebar">
        <div className="brand">
          <span>ANDJE</span>
          <strong>Chat institucional</strong>
        </div>
        <nav aria-label="Secciones de consola">
          <button
            type="button"
            className={section === 'attention' ? 'active' : ''}
            onClick={() => setSection('attention')}
          >
            Atencion
          </button>
          <button
            type="button"
            className={section === 'settings' ? 'active' : ''}
            onClick={() => setSection('settings')}
          >
            Configuracion
          </button>
        </nav>
        <div className="session-card">
          <span>Funcionario</span>
          <strong>{agentSession.agentDisplayName}</strong>
          <small>Sesion local/dev</small>
        </div>
      </aside>

      <main className="console-main">
        <header className="topbar">
          <div>
            <span className="eyebrow">MVP demo</span>
            <h1>Consola de agentes</h1>
          </div>
          <div className="topbar-actions">
            <button type="button" className="secondary-button" onClick={() => void refreshConsoleData()}>
              Actualizar
            </button>
            <button type="button" className="logout" onClick={logout}>
              Salir
            </button>
          </div>
        </header>

        {globalError && <p className="banner-error">{globalError}</p>}
        {status === 'reconectando' && (
          <p className="banner-warning">Reconectando realtime. Los datos se actualizaran al volver.</p>
        )}
        {status === 'desconectado' && (
          <p className="banner-error">Realtime desconectado. Revise el backend o recargue la consola.</p>
        )}

        <DashboardCards summary={summary} status={status} />

        {section === 'attention' ? (
          <div className="attention-grid">
            <ConversationQueue
              conversations={filteredConversations}
              selectedId={selectedId}
              unread={unread}
              filter={filter}
              search={search}
              loading={loadingConsole}
              onFilterChange={setFilter}
              onSearchChange={setSearch}
              onSelect={(id) => void selectConversation(id)}
            />
            <ConversationDetail
              selected={selected}
              messages={selectedMessages}
              notes={selectedNotes}
              tags={tags}
              quickResponses={cannedResponses}
              draft={draft}
              noteDraft={noteDraft}
              sendError={sendError}
              noteError={noteError}
              onDraftChange={setDraft}
              onNoteDraftChange={setNoteDraft}
              onSend={(event) => void sendReply(event)}
              onClose={() => void closeSelectedConversation()}
              onAddNote={(event) => void addNote(event)}
              onInsertQuickResponse={insertQuickResponse}
              onAssignTag={(tagId) => void assignConversationTag(tagId)}
              onRemoveTag={(tagId) => void removeConversationTag(tagId)}
            />
          </div>
        ) : (
          <SettingsPanel
            responses={cannedResponses}
            tags={tags}
            editingResponse={editingResponse}
            responseTitle={responseTitle}
            responseBody={responseBody}
            responseSortOrder={responseSortOrder}
            settingsError={settingsError}
            onTitleChange={setResponseTitle}
            onBodyChange={setResponseBody}
            onSortOrderChange={setResponseSortOrder}
            onEdit={editResponse}
            onCancelEdit={cancelEditResponse}
            onSubmit={(event) => void saveResponse(event)}
            onDeactivate={(responseId) => void deactivateResponse(responseId)}
          />
        )}
      </main>
    </div>
  );
}

function upsertConversation(
  conversations: ConsoleConversationDto[],
  conversation: ConversationDto,
): ConsoleConversationDto[] {
  const current = conversations.find((item) => item.id === conversation.id);
  if (current) {
    return conversations.map((item) =>
      item.id === conversation.id ? toConsoleConversation(conversation, item) : item,
    );
  }

  return [toConsoleConversation(conversation), ...conversations];
}

function matchesFilter(
  conversation: ConsoleConversationDto,
  filter: ConversationFilter,
): boolean {
  if (filter === 'all') {
    return true;
  }
  if (filter === 'open') {
    return conversation.status !== 'Closed';
  }
  if (filter === 'pending') {
    return conversation.status === 'Pending';
  }
  if (filter === 'active') {
    return conversation.status === 'Active';
  }
  return conversation.status === 'Closed';
}

function matchesSearch(
  conversation: ConsoleConversationDto,
  search: string,
  loadedMessages: ChatMessageDto[],
): boolean {
  const term = search.trim().toLowerCase();
  if (!term) {
    return true;
  }

  const haystack = [
    conversation.visitorDisplayName ?? '',
    conversation.lastMessagePreview ?? '',
    ...loadedMessages.map((message) => message.content),
  ]
    .join(' ')
    .toLowerCase();

  return haystack.includes(term);
}

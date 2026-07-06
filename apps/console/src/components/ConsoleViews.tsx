import { FormEvent } from 'react';
import {
  CannedResponseDto,
  ChatMessageDto,
  ConsoleConversationDto,
  ConsoleSummaryDto,
  ConversationFilter,
  ConversationTagDto,
  InternalNoteDto,
} from '../types';

export type ConnectionStatus = 'conectando' | 'conectado' | 'reconectando' | 'desconectado';

interface LoginViewProps {
  loginName: string;
  loginCode: string;
  loginError: string;
  loginSubmitting: boolean;
  onNameChange: (value: string) => void;
  onCodeChange: (value: string) => void;
  onSubmit: (event: FormEvent) => void;
}

export function LoginView({
  loginName,
  loginCode,
  loginError,
  loginSubmitting,
  onNameChange,
  onCodeChange,
  onSubmit,
}: LoginViewProps) {
  return (
    <main className="access-page">
      <form className="access-card" onSubmit={onSubmit}>
        <span className="eyebrow">Consola institucional</span>
        <h1>Acceso de agentes</h1>
        <p>
          Ingrese con el codigo local de demo para atender conversaciones del
          widget ciudadano.
        </p>
        <label>
          Nombre del funcionario
          <input
            type="text"
            maxLength={80}
            autoComplete="off"
            value={loginName}
            onChange={(event) => onNameChange(event.target.value)}
            placeholder="Funcionario Demo"
          />
        </label>
        <label>
          Codigo de acceso
          <input
            type="password"
            autoComplete="off"
            value={loginCode}
            onChange={(event) => onCodeChange(event.target.value)}
          />
        </label>
        <button type="submit" disabled={loginSubmitting}>
          {loginSubmitting ? 'Ingresando...' : 'Ingresar'}
        </button>
        {loginError && <p className="error">{loginError}</p>}
        <p className="access-note">
          Acceso local/dev. No es autenticacion institucional ni Entra ID.
        </p>
      </form>
    </main>
  );
}

interface DashboardCardsProps {
  summary: ConsoleSummaryDto | null;
  status: ConnectionStatus;
}

export function DashboardCards({ summary, status }: DashboardCardsProps) {
  const cards = [
    ['Abiertas', summary?.conversationsOpen ?? 0],
    ['En espera', summary?.conversationsPending ?? 0],
    ['Activas', summary?.conversationsActive ?? 0],
    ['Cerradas', summary?.conversationsClosed ?? 0],
    ['Mensajes', summary?.messagesTotal ?? 0],
  ] as const;

  return (
    <section className="dashboard" aria-label="Resumen operativo">
      {cards.map(([label, value]) => (
        <article className="metric-card" key={label}>
          <span>{label}</span>
          <strong>{value}</strong>
        </article>
      ))}
      <article className="metric-card realtime">
        <span>Realtime</span>
        <strong className={`status-dot ${status}`}>{status}</strong>
      </article>
    </section>
  );
}

interface ConversationQueueProps {
  conversations: ConsoleConversationDto[];
  selectedId: string | null;
  unread: Record<string, number>;
  filter: ConversationFilter;
  search: string;
  loading: boolean;
  onFilterChange: (filter: ConversationFilter) => void;
  onSearchChange: (value: string) => void;
  onSelect: (id: string) => void;
}

export function ConversationQueue({
  conversations,
  selectedId,
  unread,
  filter,
  search,
  loading,
  onFilterChange,
  onSearchChange,
  onSelect,
}: ConversationQueueProps) {
  const filters: Array<[ConversationFilter, string]> = [
    ['open', 'Abiertas'],
    ['pending', 'En espera'],
    ['active', 'Activas'],
    ['closed', 'Cerradas'],
    ['all', 'Todas'],
  ];

  return (
    <section className="queue-panel" aria-label="Cola de conversaciones">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Atencion</span>
          <h2>Cola de conversaciones</h2>
        </div>
        {loading && <span className="small-muted">Actualizando...</span>}
      </div>

      <div className="filter-tabs" role="tablist" aria-label="Filtros de estado">
        {filters.map(([value, label]) => (
          <button
            key={value}
            type="button"
            className={filter === value ? 'active' : ''}
            onClick={() => onFilterChange(value)}
          >
            {label}
          </button>
        ))}
      </div>

      <input
        className="search"
        type="search"
        value={search}
        placeholder="Buscar por ciudadano o mensaje..."
        onChange={(event) => onSearchChange(event.target.value)}
      />

      {conversations.length === 0 ? (
        <div className="empty-state">
          <strong>No hay conversaciones en este filtro</strong>
          <span>Inicie una desde el widget demo o cambie el filtro.</span>
        </div>
      ) : (
        <ul className="conversation-list">
          {conversations.map((conversation) => (
            <li key={conversation.id}>
              <button
                type="button"
                className={`conversation-item ${conversation.id === selectedId ? 'selected' : ''}`}
                onClick={() => onSelect(conversation.id)}
              >
                <span className="conversation-row">
                  <strong>{conversation.visitorDisplayName ?? 'Ciudadano anonimo'}</strong>
                  <StatusBadge status={conversation.status} />
                </span>
                <span className="conversation-preview">
                  {conversation.lastMessagePreview ?? 'Sin mensajes aun'}
                </span>
                <span className="conversation-row meta">
                  <span>{formatDate(conversation.lastMessageAtUtc ?? conversation.updatedAtUtc)}</span>
                  {(unread[conversation.id] ?? 0) > 0 && (
                    <span className="unread">{unread[conversation.id]}</span>
                  )}
                </span>
                <TagList tags={conversation.tags} compact />
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

interface ConversationDetailProps {
  selected: ConsoleConversationDto | null;
  messages: ChatMessageDto[];
  notes: InternalNoteDto[];
  tags: ConversationTagDto[];
  quickResponses: CannedResponseDto[];
  draft: string;
  noteDraft: string;
  sendError: string;
  noteError: string;
  onDraftChange: (value: string) => void;
  onNoteDraftChange: (value: string) => void;
  onSend: (event: FormEvent) => void;
  onClose: () => void;
  onAddNote: (event: FormEvent) => void;
  onInsertQuickResponse: (body: string) => void;
  onAssignTag: (tagId: string) => void;
  onRemoveTag: (tagId: string) => void;
}

export function ConversationDetail({
  selected,
  messages,
  notes,
  tags,
  quickResponses,
  draft,
  noteDraft,
  sendError,
  noteError,
  onDraftChange,
  onNoteDraftChange,
  onSend,
  onClose,
  onAddNote,
  onInsertQuickResponse,
  onAssignTag,
  onRemoveTag,
}: ConversationDetailProps) {
  if (!selected) {
    return (
      <section className="detail-panel empty-detail" aria-label="Detalle de conversacion">
        <div className="empty-state">
          <strong>Seleccione una conversacion</strong>
          <span>El historial, respuestas rapidas, notas y etiquetas apareceran aqui.</span>
        </div>
      </section>
    );
  }

  const isClosed = selected.status === 'Closed';
  const assignedTagIds = new Set(selected.tags.map((tag) => tag.id));
  const availableTags = tags.filter((tag) => !assignedTagIds.has(tag.id));

  return (
    <section className="detail-panel" aria-label="Detalle de conversacion">
      <header className="detail-header">
        <div>
          <span className="eyebrow">Conversacion</span>
          <h2>{selected.visitorDisplayName ?? 'Ciudadano anonimo'}</h2>
          <p>
            Creada {formatDate(selected.startedAt)}
            {selected.closedAtUtc ? ` · Cerrada ${formatDate(selected.closedAtUtc)}` : ''}
          </p>
        </div>
        <div className="detail-actions">
          <StatusBadge status={selected.status} />
          {!isClosed && (
            <button type="button" className="danger-button" onClick={onClose}>
              Cerrar
            </button>
          )}
        </div>
      </header>

      <TagList tags={selected.tags} />
      <div className="tag-picker">
        {availableTags.map((tag) => (
          <button
            key={tag.id}
            type="button"
            style={{ borderColor: tag.color, color: tag.color }}
            onClick={() => onAssignTag(tag.id)}
          >
            + {tag.name}
          </button>
        ))}
        {selected.tags.map((tag) => (
          <button key={tag.id} type="button" onClick={() => onRemoveTag(tag.id)}>
            Quitar {tag.name}
          </button>
        ))}
      </div>

      <div className="workspace-grid">
        <div className="timeline-card">
          <div className="section-heading">
            <h3>Historial</h3>
            <span className="small-muted">{messages.length} mensajes</span>
          </div>
          <div className="message-timeline">
            {messages.length === 0 ? (
              <div className="empty-state compact">
                <span>No hay mensajes cargados.</span>
              </div>
            ) : (
              messages.map((message) => (
                <article key={message.id} className={`message ${message.senderType.toLowerCase()}`}>
                  <span>{message.senderType === 'Agent' ? 'Funcionario' : 'Ciudadano'}</span>
                  <p>{message.content}</p>
                  <time>{formatDate(message.sentAt)}</time>
                </article>
              ))
            )}
          </div>

          <div className="quick-responses">
            <span>Respuestas rapidas</span>
            <div>
              {quickResponses.filter((response) => response.isActive).map((response) => (
                <button
                  key={response.id}
                  type="button"
                  disabled={isClosed}
                  onClick={() => onInsertQuickResponse(response.body)}
                >
                  {response.title}
                </button>
              ))}
            </div>
          </div>

          <form className="reply-composer" onSubmit={onSend}>
            <textarea
              value={draft}
              maxLength={2000}
              disabled={isClosed}
              placeholder={isClosed ? 'Conversacion cerrada' : 'Escriba la respuesta del funcionario...'}
              onChange={(event) => onDraftChange(event.target.value)}
            />
            <button type="submit" disabled={isClosed || draft.trim().length === 0}>
              Responder
            </button>
          </form>
          {isClosed && (
            <p className="closed-note">
              Esta conversacion esta cerrada. El historial se puede consultar,
              pero ya no se envian respuestas.
            </p>
          )}
          {sendError && <p className="error">{sendError}</p>}
        </div>

        <aside className="notes-card">
          <div className="section-heading">
            <h3>Notas internas</h3>
            <span className="small-muted">No visibles al ciudadano</span>
          </div>
          <form className="note-form" onSubmit={onAddNote}>
            <textarea
              value={noteDraft}
              maxLength={1000}
              placeholder="Agregar nota interna..."
              onChange={(event) => onNoteDraftChange(event.target.value)}
            />
            <button type="submit" disabled={noteDraft.trim().length === 0}>
              Guardar nota
            </button>
          </form>
          {noteError && <p className="error">{noteError}</p>}
          <div className="notes-list">
            {notes.length === 0 ? (
              <span className="small-muted">Sin notas internas.</span>
            ) : (
              notes.map((note) => (
                <article key={note.id} className="note-item">
                  <strong>{note.agentDisplayName}</strong>
                  <p>{note.body}</p>
                  <time>{formatDate(note.createdAtUtc)}</time>
                </article>
              ))
            )}
          </div>
        </aside>
      </div>
    </section>
  );
}

interface SettingsPanelProps {
  responses: CannedResponseDto[];
  tags: ConversationTagDto[];
  editingResponse: CannedResponseDto | null;
  responseTitle: string;
  responseBody: string;
  responseSortOrder: number;
  settingsError: string;
  onTitleChange: (value: string) => void;
  onBodyChange: (value: string) => void;
  onSortOrderChange: (value: number) => void;
  onEdit: (response: CannedResponseDto) => void;
  onCancelEdit: () => void;
  onSubmit: (event: FormEvent) => void;
  onDeactivate: (responseId: string) => void;
}

export function SettingsPanel({
  responses,
  tags,
  editingResponse,
  responseTitle,
  responseBody,
  responseSortOrder,
  settingsError,
  onTitleChange,
  onBodyChange,
  onSortOrderChange,
  onEdit,
  onCancelEdit,
  onSubmit,
  onDeactivate,
}: SettingsPanelProps) {
  return (
    <section className="settings-panel" aria-label="Configuracion">
      <div className="section-heading">
        <div>
          <span className="eyebrow">Configuracion</span>
          <h2>Respuestas rapidas y etiquetas</h2>
        </div>
      </div>

      <div className="settings-grid">
        <form className="settings-form" onSubmit={onSubmit}>
          <h3>{editingResponse ? 'Editar respuesta rapida' : 'Nueva respuesta rapida'}</h3>
          <label>
            Titulo
            <input
              value={responseTitle}
              maxLength={80}
              onChange={(event) => onTitleChange(event.target.value)}
            />
          </label>
          <label>
            Cuerpo
            <textarea
              value={responseBody}
              maxLength={2000}
              onChange={(event) => onBodyChange(event.target.value)}
            />
          </label>
          <label>
            Orden
            <input
              type="number"
              value={responseSortOrder}
              onChange={(event) => onSortOrderChange(Number(event.target.value))}
            />
          </label>
          <div className="form-actions">
            <button type="submit">
              {editingResponse ? 'Guardar cambios' : 'Crear respuesta'}
            </button>
            {editingResponse && (
              <button type="button" className="secondary-button" onClick={onCancelEdit}>
                Cancelar
              </button>
            )}
          </div>
          {settingsError && <p className="error">{settingsError}</p>}
        </form>

        <div className="settings-list">
          <h3>Respuestas configuradas</h3>
          {responses.length === 0 ? (
            <p className="small-muted">No hay respuestas rapidas.</p>
          ) : (
            responses.map((response) => (
              <article key={response.id} className={`response-item ${response.isActive ? '' : 'inactive'}`}>
                <div>
                  <strong>{response.title}</strong>
                  <p>{response.body}</p>
                  <span>Orden {response.sortOrder}</span>
                </div>
                <div className="item-actions">
                  <button type="button" onClick={() => onEdit(response)}>
                    Editar
                  </button>
                  {response.isActive && (
                    <button type="button" onClick={() => onDeactivate(response.id)}>
                      Desactivar
                    </button>
                  )}
                </div>
              </article>
            ))
          )}
        </div>

        <div className="settings-list tags-catalog">
          <h3>Etiquetas disponibles</h3>
          <TagList tags={tags} />
          <p className="small-muted">
            Catalogo demo persistido. La creacion avanzada de etiquetas queda
            para una fase posterior.
          </p>
        </div>
      </div>
    </section>
  );
}

function StatusBadge({ status }: { status: string }) {
  return <span className={`status-badge ${status.toLowerCase()}`}>{statusLabel(status)}</span>;
}

function TagList({ tags, compact = false }: { tags: ConversationTagDto[]; compact?: boolean }) {
  if (tags.length === 0) {
    return compact ? null : <span className="small-muted">Sin etiquetas asignadas.</span>;
  }

  return (
    <div className={`tag-list ${compact ? 'compact' : ''}`}>
      {tags.map((tag) => (
        <span key={tag.id} style={{ borderColor: tag.color, color: tag.color }}>
          {tag.name}
        </span>
      ))}
    </div>
  );
}

function statusLabel(status: string): string {
  if (status === 'Pending') {
    return 'En espera';
  }
  if (status === 'Active') {
    return 'Activa';
  }
  return 'Cerrada';
}

function formatDate(value: string): string {
  const timestamp = Date.parse(value);
  if (Number.isNaN(timestamp)) {
    return value;
  }

  return new Intl.DateTimeFormat('es-CO', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(timestamp));
}

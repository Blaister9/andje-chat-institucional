import {
  CannedResponseDto,
  ConsoleConversationDto,
  ConsoleSummaryDto,
  ConversationTagDto,
  InternalNoteDto,
} from './types';

export const API_BASE = import.meta.env.VITE_API_URL ?? 'http://localhost:8080';

export class SessionExpiredError extends Error {
  constructor() {
    super('Agent session expired.');
  }
}

async function apiFetch<T>(
  path: string,
  accessToken: string,
  options: RequestInit = {},
): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('Authorization', `Bearer ${accessToken}`);
  if (options.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
  });

  if (response.status === 401) {
    throw new SessionExpiredError();
  }

  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function getSummary(accessToken: string): Promise<ConsoleSummaryDto> {
  return apiFetch('/api/console/summary', accessToken);
}

export function getConsoleConversations(accessToken: string): Promise<ConsoleConversationDto[]> {
  return apiFetch('/api/console/conversations', accessToken);
}

export function getCannedResponses(accessToken: string): Promise<CannedResponseDto[]> {
  return apiFetch('/api/console/canned-responses', accessToken);
}

export function createCannedResponse(
  accessToken: string,
  title: string,
  body: string,
  sortOrder: number,
): Promise<CannedResponseDto> {
  return apiFetch('/api/console/canned-responses', accessToken, {
    method: 'POST',
    body: JSON.stringify({ title, body, sortOrder, isActive: true }),
  });
}

export function updateCannedResponse(
  accessToken: string,
  response: CannedResponseDto,
  title: string,
  body: string,
  sortOrder: number,
): Promise<CannedResponseDto> {
  return apiFetch(`/api/console/canned-responses/${response.id}`, accessToken, {
    method: 'PUT',
    body: JSON.stringify({
      title,
      body,
      sortOrder,
      isActive: response.isActive,
    }),
  });
}

export function deactivateCannedResponse(
  accessToken: string,
  responseId: string,
): Promise<CannedResponseDto> {
  return apiFetch(`/api/console/canned-responses/${responseId}/deactivate`, accessToken, {
    method: 'PATCH',
  });
}

export function getTags(accessToken: string): Promise<ConversationTagDto[]> {
  return apiFetch('/api/console/tags', accessToken);
}

export function assignTag(
  accessToken: string,
  conversationId: string,
  tagId: string,
): Promise<ConversationTagDto> {
  return apiFetch(`/api/console/conversations/${conversationId}/tags/${tagId}`, accessToken, {
    method: 'POST',
  });
}

export function removeTag(
  accessToken: string,
  conversationId: string,
  tagId: string,
): Promise<void> {
  return apiFetch(`/api/console/conversations/${conversationId}/tags/${tagId}`, accessToken, {
    method: 'DELETE',
  });
}

export function getInternalNotes(
  accessToken: string,
  conversationId: string,
): Promise<InternalNoteDto[]> {
  return apiFetch(`/api/console/conversations/${conversationId}/notes`, accessToken);
}

export function createInternalNote(
  accessToken: string,
  conversationId: string,
  body: string,
): Promise<InternalNoteDto> {
  return apiFetch(`/api/console/conversations/${conversationId}/notes`, accessToken, {
    method: 'POST',
    body: JSON.stringify({ body }),
  });
}

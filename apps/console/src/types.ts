export interface ConversationDto {
  id: string;
  status: 'Pending' | 'Active' | 'Closed';
  visitorDisplayName: string | null;
  startedAt: string;
  updatedAtUtc: string;
  closedAtUtc: string | null;
  topic?: string | null;
}

export interface ChatMessageDto {
  id: string;
  conversationId: string;
  senderType: 'Visitor' | 'Agent';
  content: string;
  sentAt: string;
}

export interface AgentSessionDto {
  accessToken: string;
  agentDisplayName: string;
  expiresAtUtc: string;
}

export interface ConversationTagDto {
  id: string;
  name: string;
  color: string;
  isActive: boolean;
}

export interface ConsoleConversationDto extends ConversationDto {
  lastMessagePreview: string | null;
  lastMessageAtUtc: string | null;
  tags: ConversationTagDto[];
  topic?: string | null;
  feedbackRating?: number | null;
  feedbackComment?: string | null;
  feedbackCreatedAtUtc?: string | null;
}

export interface ConsoleSummaryDto {
  conversationsOpen: number;
  conversationsPending: number;
  conversationsActive: number;
  conversationsClosed: number;
  messagesTotal: number;
  cannedResponsesActive: number;
  tagsActive: number;
  feedbackCount: number;
  averageRating: number | null;
  positiveFeedbackCount: number;
  positiveFeedbackRate: number | null;
  generatedAtUtc: string;
}

export interface CannedResponseDto {
  id: string;
  title: string;
  body: string;
  isActive: boolean;
  sortOrder: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface InternalNoteDto {
  id: string;
  conversationId: string;
  body: string;
  agentDisplayName: string;
  createdAtUtc: string;
}

export type ConversationFilter = 'open' | 'pending' | 'active' | 'closed' | 'all';

export type RatingFilter = 'all' | '5' | '4' | '3' | '2' | '1' | 'none';

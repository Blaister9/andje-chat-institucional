export interface ConversationDto {
  id: string;
  status: 'Pending' | 'Active' | 'Closed';
  visitorDisplayName: string | null;
  startedAt: string;
  updatedAtUtc: string;
  closedAtUtc: string | null;
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

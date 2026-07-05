export interface ConversationDto {
  id: string;
  status: 'Pending' | 'Active';
  visitorDisplayName: string | null;
  startedAt: string;
}

export interface ChatMessageDto {
  id: string;
  conversationId: string;
  senderType: 'Visitor' | 'Agent';
  content: string;
  sentAt: string;
}

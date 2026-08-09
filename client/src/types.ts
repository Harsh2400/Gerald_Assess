export interface Citation {
  docId: string;
  docTitle: string;
  chunkId: string;
  heading: string;
  snippet: string;
  startChar: number;
  endChar: number;
  bm25Score: number;
  semanticScore: number;
  rerankScore: number;
}

export interface AskResponse {
  question: string;
  answer: string;
  citations: Citation[];
  confidence: number;
  noConfidentAnswer: boolean;
}

export interface ChatMessageDto {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  citations: Citation[] | null;
  confidence: number | null;
  noConfidentAnswer: boolean;
  createdAt: string;
}

export interface ChatResponse {
  conversationId: string;
  userMessage: ChatMessageDto;
  assistantMessage: ChatMessageDto;
}

export interface ConversationSummary {
  id: string;
  createdAt: string;
  messageCount: number;
  lastMessagePreview: string | null;
}

export interface DocumentSummary {
  id: string;
  title: string;
  sourceType: string;
  chunkCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface ChunkSummary {
  id: string;
  documentId: string;
  chunkIndex: number;
  heading: string;
  text: string;
  startChar: number;
  endChar: number;
  updatedAt: string;
}

export interface DocumentDetail extends DocumentSummary {
  content: string;
  chunks: ChunkSummary[];
}

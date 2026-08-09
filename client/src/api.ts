import type {
  AskResponse,
  ChatMessageDto,
  ChatResponse,
  ChunkSummary,
  ConversationSummary,
  DocumentDetail,
  DocumentSummary,
} from './types';

const API_BASE = 'http://localhost:5252/api';

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  });
  if (!res.ok) {
    let detail = '';
    try {
      const body = await res.json();
      detail = body.error ?? JSON.stringify(body);
    } catch {
      detail = res.statusText;
    }
    throw new Error(`${res.status}: ${detail}`);
  }
  if (res.status === 204) return undefined as T;
  return res.json();
}

export const api = {
  ask: (question: string, topK = 3) =>
    request<AskResponse>('/ask', { method: 'POST', body: JSON.stringify({ question, topK }) }),

  listConversations: () => request<ConversationSummary[]>('/chat'),

  getChatHistory: (conversationId: string) =>
    request<ChatMessageDto[]>(`/chat/${conversationId}`),

  sendChatMessage: (conversationId: string | null, message: string, topK = 3) =>
    request<ChatResponse>(conversationId ? `/chat/${conversationId}` : '/chat', {
      method: 'POST',
      body: JSON.stringify({ message, topK }),
    }),

  listDocuments: () => request<DocumentSummary[]>('/documents'),

  getDocument: (id: string) => request<DocumentDetail>(`/documents/${id}`),

  createDocument: (title: string, content: string) =>
    request<DocumentDetail>('/documents', { method: 'POST', body: JSON.stringify({ title, content }) }),

  updateDocument: (id: string, title: string, content: string) =>
    request<DocumentDetail>(`/documents/${id}`, { method: 'PUT', body: JSON.stringify({ title, content }) }),

  deleteDocument: (id: string) => request<void>(`/documents/${id}`, { method: 'DELETE' }),

  listChunks: (documentId?: string) =>
    request<ChunkSummary[]>(`/chunks${documentId ? `?documentId=${documentId}` : ''}`),

  updateChunk: (id: string, text: string) =>
    request<ChunkSummary>(`/chunks/${id}`, { method: 'PUT', body: JSON.stringify({ text }) }),

  deleteChunk: (id: string) => request<void>(`/chunks/${id}`, { method: 'DELETE' }),
};

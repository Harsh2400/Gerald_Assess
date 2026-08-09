import { useEffect, useRef, useState } from 'react';
import { api } from '../api';
import type { ChatMessageDto, ConversationSummary } from '../types';
import { CitationCard } from './CitationCard';

const SAMPLE_QUESTIONS = [
  'How much does the Business plan cost per month?',
  'How long until deleted files are permanently purged?',
  'What is the response time for Enterprise support?',
  'What is the airspeed velocity of an unladen swallow?',
];

export function ChatView() {
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessageDto[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const refreshConversations = () => {
    api.listConversations().then(setConversations).catch(() => {});
  };

  useEffect(() => {
    refreshConversations();
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  async function loadConversation(id: string) {
    setError(null);
    try {
      const history = await api.getChatHistory(id);
      setMessages(history);
      setActiveConversationId(id);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load conversation.');
    }
  }

  function startNewConversation() {
    setActiveConversationId(null);
    setMessages([]);
    setError(null);
  }

  async function send(text: string) {
    if (!text.trim() || loading) return;
    setLoading(true);
    setError(null);
    setInput('');
    try {
      const res = await api.sendChatMessage(activeConversationId, text.trim());
      setMessages((prev) => [...prev, res.userMessage, res.assistantMessage]);
      if (!activeConversationId) {
        setActiveConversationId(res.conversationId);
      }
      refreshConversations();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="chat-layout">
      <div className="chat-sidebar">
        <div className="chat-sidebar-header">
          <button className="btn btn-primary" style={{ width: '100%' }} onClick={startNewConversation}>
            + New conversation
          </button>
        </div>
        {conversations.length === 0 && <div className="empty-state">No conversations yet</div>}
        {conversations.map((c) => (
          <div
            key={c.id}
            className={`conversation-item ${c.id === activeConversationId ? 'active' : ''}`}
            onClick={() => loadConversation(c.id)}
          >
            <div>{c.messageCount} messages</div>
            <div className="preview">{c.lastMessagePreview ?? '(empty)'}</div>
          </div>
        ))}
      </div>

      <div className="chat-main">
        <div className="chat-messages">
          {messages.length === 0 && (
            <div className="chat-intro">
              <h2>Ask the knowledge base</h2>
              <p>Answers are grounded in indexed documents, with citations and confidence scores.</p>
              <div className="sample-chips">
                {SAMPLE_QUESTIONS.map((q) => (
                  <button key={q} onClick={() => send(q)}>
                    {q}
                  </button>
                ))}
              </div>
            </div>
          )}

          {messages.map((m) => (
            <div key={m.id} className={`message-row ${m.role}`}>
              {m.role === 'user' ? (
                <div className="message-bubble">{m.content}</div>
              ) : (
                <div className="message-bubble-wrap">
                  <div className={`message-bubble ${m.noConfidentAnswer ? 'no-answer' : ''}`}>
                    {m.content}
                    <div style={{ marginTop: 8 }}>
                      <span
                        className={`pill pill-confidence ${m.noConfidentAnswer ? 'low' : ''}`}
                      >
                        confidence {(m.confidence ?? 0).toFixed(2)}
                        {m.noConfidentAnswer ? ' · no confident answer' : ''}
                      </span>
                    </div>
                  </div>
                  {m.citations && m.citations.length > 0 && (
                    <div className="citations-block">
                      {m.citations.map((c) => (
                        <CitationCard key={c.chunkId} citation={c} />
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          ))}
          <div ref={messagesEndRef} />
        </div>

        {error && (
          <div style={{ padding: '0 24px' }}>
            <div className="error-banner">{error}</div>
          </div>
        )}

        <div className="chat-composer">
          <form
            onSubmit={(e) => {
              e.preventDefault();
              send(input);
            }}
          >
            <input
              className="text-input"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder="Ask a question about the knowledge base..."
            />
            <button className="btn btn-primary" type="submit" disabled={loading}>
              {loading ? 'Asking...' : 'Ask'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}

import { useEffect, useRef, useState } from 'react';
import { api } from '../api';
import type { ChatMessageDto, ConversationSummary } from '../types';
import { CitationCard } from './CitationCard';

const SAMPLE_QUESTIONS = [
  'Explain the document in a concise summary.',
  'What are the top key points I should know?',
  'What are the main conclusions and takeaways?',
];

function formatTime(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? 'Just now' : date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

export function ChatView() {
  const [conversations, setConversations] = useState<ConversationSummary[]>([]);
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessageDto[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const refreshConversations = () => api.listConversations().then(setConversations).catch(() => {});
  useEffect(() => { refreshConversations(); }, []);
  useEffect(() => { messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' }); }, [messages, loading]);

  async function loadConversation(id: string) {
    setError(null);
    setHistoryOpen(false);
    try {
      setMessages(await api.getChatHistory(id));
      setActiveConversationId(id);
    } catch (err) { setError(err instanceof Error ? err.message : 'Failed to load conversation.'); }
  }

  function startNewConversation() {
    setActiveConversationId(null);
    setMessages([]);
    setError(null);
    setHistoryOpen(false);
  }

  async function send(text: string) {
    if (!text.trim() || loading) return;
    setLoading(true); setError(null); setInput('');
    try {
      const res = await api.sendChatMessage(activeConversationId, text.trim());
      setMessages((prev) => [...prev, res.userMessage, res.assistantMessage]);
      if (!activeConversationId) setActiveConversationId(res.conversationId);
      refreshConversations();
    } catch (err) { setError(err instanceof Error ? err.message : 'Something went wrong.'); }
    finally { setLoading(false); }
  }

  return (
    <div className="chat-main">
      <header className="assistant-header">
        <div className="assistant-mark">✦</div>
        <div><h2>AI Assistant</h2><p><i/> RAG active</p></div>
        <div className="assistant-actions">
          <button title="Conversation history" onClick={() => setHistoryOpen((v) => !v)}>⌁</button>
          <button title="New conversation" onClick={startNewConversation}>＋</button>
        </div>
        {historyOpen && (
          <div className="history-popover">
            <div className="history-title">Recent conversations</div>
            {conversations.length === 0 && <p>No conversations yet</p>}
            {conversations.map((c) => <button key={c.id} onClick={() => loadConversation(c.id)} className={c.id === activeConversationId ? 'active' : ''}><span>{c.lastMessagePreview || 'New conversation'}</span><small>{c.messageCount} messages</small></button>)}
          </div>
        )}
      </header>

      <div className="chat-messages">
        {messages.length === 0 && (
          <div className="chat-intro">
            <div className="intro-orb">✦</div>
            <h2>Ask your knowledge base</h2>
            <p>I’ll answer from your indexed documents and show exactly where the answer came from.</p>
            <div className="sample-chips">{SAMPLE_QUESTIONS.map((q) => <button key={q} onClick={() => send(q)}>{q}<span>→</span></button>)}</div>
          </div>
        )}
        {messages.map((m) => (
          <div key={m.id} className={`message-row ${m.role}`}>
            {m.role === 'assistant' && <div className="message-avatar">✦</div>}
            <div className="message-content">
              <div className={`message-bubble ${m.noConfidentAnswer ? 'no-answer' : ''}`}>{m.content}</div>
              {m.role === 'assistant' && m.citations && m.citations.length > 0 && <div className="citations-block">{m.citations.map((c) => <CitationCard key={c.chunkId} citation={c}/>)}</div>}
              <div className="message-meta">{formatTime(m.createdAt)}{m.role === 'assistant' && m.confidence !== null ? ` · ${Math.round(m.confidence * 100)}% confidence` : ''}</div>
            </div>
            {m.role === 'user' && <div className="user-avatar">You</div>}
          </div>
        ))}
        {loading && <div className="message-row assistant"><div className="message-avatar">✦</div><div className="thinking"><i/><i/><i/><span>Searching your knowledge…</span></div></div>}
        <div ref={messagesEndRef}/>
      </div>
      {error && <div className="inline-error">{error}<button onClick={() => setError(null)}>×</button></div>}
      <div className="chat-composer">
        <form onSubmit={(e) => { e.preventDefault(); send(input); }}>
          <input value={input} onChange={(e) => setInput(e.target.value)} placeholder="Ask a question about your documents…" aria-label="Message AI assistant"/>
          <button className="send-button" type="submit" disabled={loading || !input.trim()} aria-label="Send message">➤</button>
        </form>
        <p>AI can make mistakes. Verify important information.</p>
      </div>
    </div>
  );
}

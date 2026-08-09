import { useEffect, useState } from 'react';

const API_BASE = 'http://localhost:5252/api';

interface Citation {
  docId: string;
  docTitle: string;
  snippet: string;
  score: number;
}

interface AskResponse {
  question: string;
  answer: string;
  citations: Citation[];
  confidence: number;
  noConfidentAnswer: boolean;
}

const SAMPLE_QUESTIONS = [
  'How much does the Business plan cost per month?',
  'How long until deleted files are permanently purged?',
  'What is the response time for Enterprise support?',
  'What is the airspeed velocity of an unladen swallow?',
];

function App() {
  const [question, setQuestion] = useState('');
  const [result, setResult] = useState<AskResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [docTitles, setDocTitles] = useState<string[]>([]);

  useEffect(() => {
    fetch(`${API_BASE}/docs`)
      .then((res) => res.json())
      .then((data) => setDocTitles(data.documentTitles ?? []))
      .catch(() => setDocTitles([]));
  }, []);

  async function ask(q: string) {
    if (!q.trim() || loading) return;
    setLoading(true);
    setError(null);
    setResult(null);
    try {
      const res = await fetch(`${API_BASE}/ask`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ question: q, topK: 3 }),
      });
      if (!res.ok) {
        throw new Error(`Request failed with status ${res.status}`);
      }
      const data: AskResponse = await res.json();
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <h1>Aurora Knowledge Assistant</h1>
      <p className="subtitle">
        A small RAG service over a handful of sample support docs. Ask a question and
        get an answer grounded in cited source snippets.
      </p>

      <form
        className="ask-form"
        onSubmit={(e) => {
          e.preventDefault();
          ask(question);
        }}
      >
        <input
          type="text"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="Ask something about Aurora Cloud Storage..."
        />
        <button type="submit" disabled={loading}>
          {loading ? 'Asking...' : 'Ask'}
        </button>
      </form>

      <div className="sample-questions">
        {SAMPLE_QUESTIONS.map((q) => (
          <button
            key={q}
            type="button"
            onClick={() => {
              setQuestion(q);
              ask(q);
            }}
          >
            {q}
          </button>
        ))}
      </div>

      {error && <div className="error-banner">{error}</div>}

      {result && (
        <div className={`answer-card ${result.noConfidentAnswer ? 'no-answer' : ''}`}>
          <div className="answer-question">Q: {result.question}</div>
          <div className="answer-text">{result.answer}</div>
          <span className="confidence-badge">
            confidence {result.confidence.toFixed(2)}
            {result.noConfidentAnswer ? ' · no confident answer' : ''}
          </span>

          {result.citations.length > 0 && (
            <>
              <p className="citations-title">Sources</p>
              {result.citations.map((c, i) => (
                <div className="citation" key={`${c.docId}-${i}`}>
                  <div className="citation-header">
                    <span>{c.docTitle}</span>
                    <span>{c.score.toFixed(2)}</span>
                  </div>
                  <div className="citation-snippet">{c.snippet}</div>
                </div>
              ))}
            </>
          )}
        </div>
      )}

      {docTitles.length > 0 && (
        <p className="indexed-docs">Indexed documents: {docTitles.join(', ')}</p>
      )}
    </>
  );
}

export default App;

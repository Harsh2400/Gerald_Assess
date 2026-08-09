import type { Citation } from '../types';

export function CitationCard({ citation }: { citation: Citation }) {
  const hasPinpoint = citation.startChar >= 0 && citation.endChar >= 0;

  return (
    <div className="citation-card">
      <div className="citation-card-header">
        <span className="citation-card-title">
          {citation.docTitle} &middot; {citation.heading}
        </span>
        <span className="citation-card-pinpoint">
          {hasPinpoint ? `chars ${citation.startChar}–${citation.endChar}` : 'edited (offset stale)'}
        </span>
      </div>
      <div className="citation-card-snippet">{citation.snippet}</div>
      <div className="score-row">
        <span className="pill" title="Keyword match strength (Okapi BM25, higher is stronger)">
          BM25 {citation.bm25Score.toFixed(2)}
        </span>
        <span className="pill" title="Semantic (vector) cosine similarity, 0–1">
          Semantic {citation.semanticScore.toFixed(2)}
        </span>
        <span className="pill" title="Final reranked relevance score, 0–1">
          Rerank {citation.rerankScore.toFixed(2)}
        </span>
      </div>
    </div>
  );
}

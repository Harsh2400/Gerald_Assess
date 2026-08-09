# Aurora Knowledge Assistant — RAG Knowledge Service

A small RAG (retrieval-augmented generation) service: it ingests a folder of markdown
knowledge articles, indexes them, and exposes an `/api/ask` endpoint that answers
questions grounded in those docs with citations and a confidence signal.

Sample dataset: five fictional support docs for a made-up product, "Aurora Cloud
Storage" (`/docs`), covering pricing, security, API rate limits, support hours, and
backup/recovery. No real company, customer, or personal data is used.

## Stack and why

- **ASP.NET Core 8 Web API (C#)** for the backend — matches the suggested stack and
  my day-to-day stack, so I could move fastest in the time box.
- **React + Vite + TypeScript** for a thin client — a single page, no routing/state
  library, since the brief explicitly allows a "thin client."
- **No vector database.** Chunks and their embeddings live in an in-memory
  `ConcurrentBag` populated once at startup. For a demo corpus of a few dozen
  chunks, brute-force cosine similarity is simpler, faster to build, and just as
  correct as standing up SQL Server's (very new/preview) vector type or an external
  vector store — see [Scaling](#scaling-thoughts) for what replaces it in production.
- **No LLM/embedding API key.** Both are stubbed behind interfaces (see below) so the
  service is fully runnable offline and deterministically testable.

## Architecture

```
docs/*.md
    │
    ▼
IngestionService        — reads files, splits into section-level chunks (MarkdownChunker)
    │
    ▼
IEmbeddingService        — embeds each chunk  (stub: HashingEmbeddingService)
    │
    ▼
IKnowledgeStore          — in-memory chunk + embedding store (InMemoryKnowledgeStore)
    │
    ▼ (at query time)
IRetrievalService         — embeds the question, cosine-similarity top-k over the store
    │
    ▼
AskService                — confidence gate, then calls generation
    │
    ▼
ILlmService                — grounded answer from the retrieved chunks (stub: ExtractiveStubLlmService)
    │
    ▼
AskController /api/ask    — HTTP contract
```

Ingestion, retrieval, and generation are separate interfaces/classes, each swappable
independently:

- `IEmbeddingService` — swap `HashingEmbeddingService` for an OpenAI/Azure
  OpenAI/Anthropic-backed embedding client with no other code changes.
- `ILlmService` — swap `ExtractiveStubLlmService` for a real chat-completion call
  (same signature: question + retrieved chunks in, answer text out).
- `IKnowledgeStore` — swap the in-memory store for a real vector store.

### Chunking

`MarkdownChunker` splits each doc on `## ` headings, so each chunk is one coherent
section (e.g. "Pricing Plans — Enterprise Plan") rather than an arbitrary fixed-size
window. Sections longer than 120 words are further split with a 20-word sliding
overlap so no single chunk grows unbounded. This is a deliberate trade: it relies on
the sample docs being reasonably well-structured markdown, which is realistic for a
docs corpus but wouldn't hold for unstructured plain text.

### Embedding stub

`HashingEmbeddingService` is a deterministic "hashing trick" bag-of-words vectorizer
(2048 dims, FNV-1a hash, random-sign feature hashing, L2-normalized) — no model, no
network call, same output every time for the same text. It's a legitimate lightweight
IR technique (not just a random placeholder): texts sharing vocabulary land close
together under cosine similarity, which is enough to demonstrate real retrieval
behavior end to end. It is not as good as a real embedding model at handling synonyms
or paraphrasing.

### Generation stub

`ExtractiveStubLlmService` doesn't generate free text — it picks the sentence in the
top-ranked chunk that shares the most words with the question. This guarantees the
answer is a substring of retrieved source text (i.e., grounded by construction),
which is the property a real LLM call has to be *prompted* for. Swapping in a real
LLM would trade "guaranteed grounded" for "more natural language" and would need a
prompt that explicitly instructs the model to answer only from the provided context
and to say so when it can't.

### Confidence / "no good answer" signal

`AskService` uses the top-1 cosine similarity score as a confidence signal, gated at
a threshold (`0.18`, tuned empirically against the sample corpus — see
`AskService.ConfidenceThreshold`). Below the threshold, the endpoint returns
`noConfidentAnswer: true` with no citations instead of forcing an answer from
irrelevant chunks.

## API contract

`POST /api/ask`

```json
// request
{ "question": "How much does the Business plan cost per month?", "topK": 3 }

// response (200)
{
  "question": "How much does the Business plan cost per month?",
  "answer": "Aurora Cloud Storage - Pricing Plans — Business Plan\nThe Business plan costs $49 per month... (from \"Aurora Cloud Storage - Pricing Plans\")",
  "citations": [
    { "docId": "pricing", "docTitle": "Aurora Cloud Storage - Pricing Plans", "snippet": "...", "score": 0.48 }
  ],
  "confidence": 0.48,
  "noConfidentAnswer": false
}
```

- `400 Bad Request` — empty/missing `question`.
- `200` with `noConfidentAnswer: true` and an empty `citations` array — retrieval
  score too low to ground an answer (this is a normal outcome, not an error, so it's
  still a 200).

`GET /api/docs` — lists ingested document titles and chunk count; used by the demo UI
to show what's indexed.

## Running it

Requires .NET 8 SDK and Node 18+.

```bash
# API — runs on http://localhost:5252, ingests /docs at startup
cd server
dotnet run

# Client — runs on http://localhost:5173
cd client
npm install
npm run dev
```

Open `http://localhost:5173`, use one of the sample-question chips, or type your own.

## Verifying the definition of done

Three grounded questions and one nonsense query, run against `/api/ask`:

| Question | Confidence | Result |
|---|---|---|
| "How much does the Business plan cost per month?" | 0.48 | Grounded answer, cites `pricing` |
| "How long until deleted files are permanently purged?" | 0.24 | Grounded answer, cites `security` |
| "What is the response time for Enterprise support?" | 0.48 | Grounded answer, cites `support-hours` |
| "What is the airspeed velocity of an unladen swallow?" | 0.07 | `noConfidentAnswer: true`, no citations |

## Scaling thoughts

For a demo corpus of ~26 chunks, brute-force in-memory cosine similarity is O(n) per
query and effectively instant. That stops being true well before a real knowledge
base's size:

- **Low thousands of chunks**: still fine in-memory, but should move out of process
  (a stateless API replica would otherwise re-ingest and re-embed on every restart) —
  persist chunks + embeddings in SQL Server/Postgres and rebuild the in-memory index
  from there on boot.
- **Tens of thousands+**: swap `IKnowledgeStore` for a real vector index —
  **pgvector** (if already on Postgres), **Azure AI Search** (native fit for an
  Azure-hosted .NET API, supports hybrid keyword+vector search and metadata
  filtering), or **Azure SQL/SQL Server's newer vector type** to stay on the
  suggested stack. Any of these swap in behind the existing `IKnowledgeStore`
  interface without touching ingestion, retrieval orchestration, or the API contract.
- **Embeddings**: swap the hashing stub for a real embedding model behind
  `IEmbeddingService` — this is the single highest-value upgrade, since hashed
  bag-of-words has no notion of synonyms or semantic similarity.
- **Re-ingestion**: currently a full re-ingest on process startup. At real scale this
  becomes an incremental pipeline (hash each doc, only re-embed changed docs) rather
  than a full rebuild.

## What I'd improve with more time

- Replace the hashing embedding stub with a real embedding model (OpenAI
  `text-embedding-3-small` or similar) — the interface is already there.
- Replace the extractive answer stub with an actual LLM call, prompted to answer only
  from provided context and to explicitly decline when context is insufficient.
- Chunk-level citation highlighting in the UI (currently shows the whole matched
  snippet, not the specific sentence used).
- Basic retrieval evals: a small fixed set of question → expected-doc-ID pairs run as
  an automated test, so chunking/embedding changes can't silently regress retrieval.
- Hybrid retrieval (keyword/BM25 + vector) — the hashing stub is weak on exact-term
  matches (e.g., product names) that a keyword pass would catch reliably.
- Persist the index (SQL Server) instead of re-ingesting from disk on every restart.

## AI tooling disclosure

Built with Claude Code (Sonnet 5), which wrote the ingestion/retrieval/generation
services, the controllers, and the React client based on my direction on
architecture and tradeoffs (in-memory store vs. real vector DB, hashing-trick stub
vs. leaving embeddings unimplemented, extractive vs. templated stub answers,
section-based vs. fixed-window chunking).

What I verified by hand: read every file end to end; ran the API and hit `/api/ask`
directly with `curl` for the three grounded questions and three different nonsense
queries to confirm the confidence threshold actually separates them (initially it
didn't — the first hashing-embedding dimension size and threshold let a "tell me a
joke about cats" query through as a false positive, so we increased the embedding
dimensionality and re-tuned the threshold against real output rather than guessing);
drove the running client through Playwright to confirm both the grounded-answer and
no-confident-answer UI states render correctly with zero console errors.

With another 2 hours: real embeddings + real LLM call behind the existing
interfaces, a retrieval eval test, and hybrid keyword+vector search.

# Aurora Knowledge Assistant — Hybrid RAG Knowledge Service

A RAG (retrieval-augmented generation) service with full document/chunk lifecycle
management: ingest, chunk, index, retrieve (hybrid BM25 + semantic + rerank), answer
with citations and confidence, and a chat interface to query and manage the
knowledge base — all backed by persistent storage instead of an in-memory demo store.

Sample dataset: six fictional support docs for a made-up product, "Aurora Cloud
Storage" (`/docs`, loaded on first run). No real company, customer, or personal data
is used.

> **Note on scope:** this project intentionally goes beyond a minimal take-home
> scope — persistence, hybrid retrieval, reranking, and full CRUD were built out
> deliberately, not because a smaller version wasn't possible. See
> [Known limitations](#known-limitations) for an honest accounting of what's still a
> stub versus production-real.

## Stack and why

- **ASP.NET Core 8 Web API (C#)** — matches my day-to-day stack.
- **SQLite via EF Core** — real persistence (documents/chunks/conversations survive
  restarts and support real CRUD) without standing up a database server. `Data
  Source=rag.db` is a one-line swap to SQL Server/Postgres in `Program.cs`; nothing
  above the `AppDbContext` layer would need to change.
- **React + Vite + TypeScript**, light-themed, three views (Chat / Documents /
  Chunks) instead of a single demo page.
- **No external vector database.** Embeddings and the BM25 index live in an
  in-memory read model (`SearchIndexService`) rebuilt from SQLite after every write.
  See [Scaling](#scaling-thoughts) for what replaces this at real scale.
- **No LLM/embedding/reranker API key.** All three are stubbed behind interfaces
  (below) so the service is fully runnable offline and deterministically testable —
  and, unlike a real model call, every stub decision is inspectable and explainable.

## Architecture

```
docs/*.md (first-run seed)          POST /api/documents (manual ingestion)
        │                                       │
        └───────────────┬───────────────────────┘
                         ▼
                 DocumentService
      MarkdownChunker.Parse → section-level chunks
      with exact char offsets into the source doc
                         │
                         ▼
              IEmbeddingService.Embed (stub: HashingEmbeddingService)
                         │
                         ▼
              SQLite (Documents, Chunks tables) ── source of truth
                         │
                         ▼ RefreshAsync() after every write
              SearchIndexService (in-memory read model)
              chunk list + Bm25Index, rebuilt from SQLite
                         │
        ┌────────────────┴────────────────┐
        ▼ (at query time)                 ▼
   Bm25Index.ScoreAll(query)      cosine similarity over embeddings
   (keyword search)                (semantic search)
        └────────────────┬────────────────┘
                          ▼
              Reciprocal Rank Fusion (RRF)
                          ▼
              IRerankerService (stub: HeuristicRerankerService)
                          ▼
              RagQueryService: confidence gate, then generation
                          ▼
              ILlmService (stub: ExtractiveStubLlmService)
                          ▼
        AskController /api/ask  ·  ChatController /api/chat (+ persistence)
```

Every stage is a separately swappable interface:

| Interface | Stub implementation | Real swap |
|---|---|---|
| `IEmbeddingService` | `HashingEmbeddingService` (hashed bag-of-words) | OpenAI/Azure OpenAI/Anthropic embedding client |
| `ILlmService` | `ExtractiveStubLlmService` (best-matching sentence) | Chat-completion call, prompted to answer only from context |
| `IRerankerService` | `HeuristicRerankerService` (lexical-overlap + fused score) | Cohere Rerank / a BGE or ms-marco cross-encoder |
| `ISearchIndexService` | In-memory, rebuilt from SQLite | pgvector / Azure AI Search / Cosmos DB vector search |

### Chunking and exact pinpoint

`MarkdownChunker` splits each doc on `## ` headings (one chunk per coherent
section), further splitting sections over 120 words with a 20-word overlap. Every
chunk carries `StartChar`/`EndChar` offsets into the parent document's stored
content, computed by tracking a cursor through the source text during parsing — not
guessed after the fact. A citation's `startChar`/`endChar` slice the original
document's `content` field exactly; this is verified in testing (see below). If a
chunk is later hand-edited via the Chunks UI, its offsets reset to `-1`/`-1` rather
than silently pointing at stale text.

### Hybrid retrieval: BM25 + semantic + RRF + rerank

Two independent rankers run per query:

- **BM25** (`Bm25Index`, Okapi BM25, k1=1.5, b=0.75) — classic inverted-index keyword
  search. Exact on terms; this is what catches a product name or error code that a
  small hashed embedding can blur.
- **Semantic** — cosine similarity over `HashingEmbeddingService` output.

Their rankings are combined with **Reciprocal Rank Fusion**
(`score = Σ 1/(k + rank + 1)`, k=60) — the same fusion technique Elasticsearch, Azure
AI Search, and Weaviate use for hybrid search. RRF works on *ranks*, not raw scores,
which sidesteps the fact that a BM25 score and a cosine similarity live on
incompatible scales and can't be averaged directly.

The fused candidate set is then **reranked** (`HeuristicRerankerService`) using the
normalized fused score, lexical token overlap, and an exact-phrase-match bonus — a
deterministic approximation of what a real cross-encoder reranker scores. The fused
score is normalized against a fixed theoretical maximum (rank #1 in both rankers),
not the batch's own min/max — an earlier version normalized per-batch and it broke
the confidence signal: the best-of-a-bad-lot candidate always looked artificially
confident. See [AI tooling disclosure](#ai-tooling-disclosure) for how that surfaced.

### Confidence / "no good answer" signal

The top reranked candidate's score (0–1) is gated at `ConfidenceThreshold = 0.35` in
`RagQueryService`, tuned empirically: grounded queries against the sample corpus
score 0.55–0.78, nonsense queries score ~0.25. Below the threshold, `/api/ask` and
`/api/chat` return `noConfidentAnswer: true` with no citations rather than forcing an
answer.

## API contract

**Q&A**

- `POST /api/ask` `{ question, topK? }` → `{ question, answer, citations[], confidence, noConfidentAnswer }`. Stateless, no conversation persisted.
- `POST /api/chat` `{ message, topK? }` → starts a new conversation; same shape wrapped as `{ conversationId, userMessage, assistantMessage }`.
- `POST /api/chat/{conversationId}` — continues an existing conversation. `404` if it doesn't exist.
- `GET /api/chat` → list conversations (id, message count, last-message preview).
- `GET /api/chat/{conversationId}` → full message history.

Each `citation` includes `docId`, `docTitle`, `chunkId`, `heading`, `snippet`,
`startChar`/`endChar` (exact pinpoint, `-1` if stale), and the full score breakdown:
`bm25Score`, `semanticScore`, `rerankScore`.

**Documents** (`/api/documents`)

- `GET /` — list (title, chunk count, source type, timestamps).
- `GET /{id}` — full detail including content and all chunks.
- `POST /` `{ title, content }` — chunks, embeds, and indexes immediately. `201`.
- `PUT /{id}` `{ title, content }` — replaces all chunks and re-embeds. `200`/`404`.
- `DELETE /{id}` — cascades to chunks. `204`/`404`.

**Chunks** (`/api/chunks`)

- `GET /?documentId=` — list, optionally filtered.
- `GET /{id}` — single chunk.
- `PUT /{id}` `{ text }` — re-embeds just this chunk; resets its offsets to `-1`/`-1`.
- `DELETE /{id}`.

All mutating endpoints return `400` on missing required fields.

## Running it

Requires .NET 8 SDK and Node 18+.

```bash
# API — http://localhost:5252. Creates rag.db and seeds /docs on first run only.
cd server
dotnet run

# Client — http://localhost:5173
cd client
npm install
npm run dev
```

Open `http://localhost:5173`. **Chat** answers questions with citations and scores.
**Documents** lists/adds/edits/deletes source documents (editing re-chunks and
re-embeds). **Chunks** browses/edits/deletes individual indexed chunks, filterable
by document.

To reset to a clean seeded state: stop the server, delete `server/rag.db*`, restart.

## Verifying the definition of done

Four questions run against `/api/ask` (see [AI tooling disclosure](#ai-tooling-disclosure)
for how the confidence threshold was actually tuned, not just asserted):

| Question | Confidence | Result |
|---|---|---|
| "How much does the Business plan cost per month?" | 0.68 | Grounded, cites `pricing`, BM25 6.85 / semantic 0.48 |
| "How long until deleted files are permanently purged?" | 0.70 | Grounded, cites `security` |
| "What is the response time for Enterprise support?" | 0.78 | Grounded, cites `support-hours` |
| "What is the airspeed velocity of an unladen swallow?" | 0.25 | `noConfidentAnswer: true`, no citations |

Also verified: citation `startChar`/`endChar` slice the exact cited text out of the
document's stored content (not just "somewhere in this doc"); document create/edit
immediately affects retrieval with no restart; chunk edit re-embeds and flips its
offsets to stale; document delete cascades and removes it from retrieval; multi-turn
chat persists and reloads correctly; all four browser-driven flows (chat, add
document, filter/edit chunk) verified with zero console errors via a scripted
Playwright pass, screenshotted at each step.

## Known limitations

Being direct about what's still a stub, since the challenge asks for exactly that:

- **Hashed embeddings have no real semantics.** `HashingEmbeddingService` is a
  hashed bag-of-words — it catches vocabulary overlap, not meaning. Concretely: after
  deleting a "Mobile App / Offline Access" document, asking about mobile offline
  storage still returned a *plausible-looking but wrong* answer (confidence 0.56,
  above threshold) grounded in a Pricing chunk, because "storage" and "device" appear
  in both. A real embedding model would separate these; no threshold on this stub can
  fully fix it, since the false positive's score overlaps the true-positive range.
- **The reranker and LLM are heuristics, not learned models.** They're deterministic
  and explainable by design, but a real cross-encoder and a real LLM call would both
  meaningfully outperform them on paraphrase and multi-hop questions.
- **Full index rebuild on every write.** `SearchIndexService.RefreshAsync()` reloads
  every chunk from SQLite and rebuilds BM25 from scratch after each create/update/
  delete. Fine at this corpus size; not how you'd do it past a few thousand chunks.
- **No multi-turn context fusion.** Each chat message is answered independently — a
  follow-up like "what about the Starter plan?" only works because it happens to be a
  complete question on its own, not because prior turns are folded into retrieval.
- **No auth/multi-tenancy.** Anyone hitting the API can read/write any document.

## Scaling thoughts

- **Vector search**: swap `ISearchIndexService`'s embedding-similarity half for
  **pgvector** (if already on Postgres) or **Azure AI Search** (native fit for an
  Azure-hosted .NET API — also supports hybrid keyword+vector search server-side,
  which would let BM25 + semantic fusion move out of process too).
- **Keyword search**: past a few thousand chunks, `Bm25Index`'s in-memory inverted
  index should become **Elasticsearch/OpenSearch** or Azure AI Search's built-in BM25,
  both of which also solve incremental indexing (this app currently does a full
  rebuild per write).
- **Embeddings**: swap `HashingEmbeddingService` for a real model — the single
  highest-value upgrade, since it's the root cause of every retrieval-quality issue
  found during testing.
- **Reranking**: swap `HeuristicRerankerService` for Cohere Rerank or a hosted
  cross-encoder; the interface contract (query + candidates in, scored+ordered list
  out) doesn't change.
- **Persistence**: SQLite → SQL Server/Postgres is a one-line connection-string
  change in `Program.cs`; the EF Core model doesn't reference anything SQLite-specific.

## What I'd improve with more time

- Real embedding model + real LLM call + real reranker behind the existing interfaces.
- A small fixed retrieval eval set (question → expected doc/chunk ID) run as an
  automated test, so future chunking/scoring changes can't silently regress quality.
- Multi-turn context fusion for chat (fold recent turns into the retrieval query).
- Incremental index updates instead of full rebuild-on-write.
- Basic auth and per-document soft-delete/versioning instead of hard delete.

## AI tooling disclosure

Built with Claude Code (Sonnet 5), which wrote the service/controller/UI code based
on my direction on architecture (hybrid retrieval design, persistence model, stub
boundaries, chunking strategy).

What I verified by hand, including bugs actually caught during that verification
(not just "I ran it once and it looked fine"):

- **Chunker offset overflow**: the char-offset cursor assumed every line was
  followed by `\n`, which is false for a file's last line — crashed on startup until
  caught by actually running the seed and reading the stack trace.
- **Reranker confidence bug**: normalizing the fused BM25+semantic score against the
  candidate *batch's* own min/max meant the best candidate in any batch always scored
  ≈1.0 — so a nonsense query still returned a "confident" answer. Caught by testing a
  deliberately irrelevant query ("tell me a joke about cats") against the running
  API, not by inspection. Fixed by normalizing against a fixed theoretical maximum
  instead.
- **Extractive-answer bug**: the stub answer generator scored sentences including the
  `"Title — Heading\n"` prefix baked into each chunk's text, so heading keywords
  leaked into scoring and could outrank the actually-relevant sentence. Caught by
  asking a specific numeric question ("how much offline storage per device") and
  noticing the returned sentence didn't contain the number.
- Every CRUD endpoint exercised directly via `curl` (create/update/delete documents
  and chunks, cascade deletes, 400/404 paths, multi-turn chat persistence).
- Full UI verified in an actual browser via a scripted Playwright pass (not just
  "it compiles") across all three tabs, with `console --errors` checked at each step
  and screenshots captured.

With another 2 hours: real embeddings behind `IEmbeddingService` first, since that's
the one stub whose limitations showed up repeatedly during testing.

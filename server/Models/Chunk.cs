namespace RagKnowledgeService.Models;

// A single indexed unit of text: one document is split into several of these.
// The actual embedding vector lives in Qdrant, keyed by Id - this is just the
// text/metadata half, used for BM25 scoring, prompts, and citations.
// StartChar/EndChar pinpoint this chunk's exact location in the source document's
// content (-1/-1 if the chunk was hand-edited and offsets are no longer valid).
public class Chunk
{
    public required string Id { get; init; }
    public required string DocId { get; init; }
    public required string DocTitle { get; init; }
    public required int ChunkIndex { get; init; }
    public required string Heading { get; init; }
    public required string Text { get; init; }
    public required int StartChar { get; init; }
    public required int EndChar { get; init; }
}

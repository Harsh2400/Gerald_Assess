namespace RagKnowledgeService.Models;

public class AskRequest
{
    public required string Question { get; init; }
    public int TopK { get; init; } = 3;
}

public class Citation
{
    public required string DocId { get; init; }
    public required string DocTitle { get; init; }
    public required string ChunkId { get; init; }
    public required string Heading { get; init; }
    public required string Snippet { get; init; }

    // Exact pinpoint into the source document's content; -1/-1 if the chunk
    // was hand-edited after ingestion and offsets are no longer authoritative.
    public required int StartChar { get; init; }
    public required int EndChar { get; init; }

    // Score breakdown from each retrieval stage, so a caller can see *why*
    // this chunk was cited rather than just trusting a single opaque number.
    public required double Bm25Score { get; init; }
    public required double SemanticScore { get; init; }
    public required double RerankScore { get; init; }
}

public class AskResponse
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public required List<Citation> Citations { get; init; }
    public required double Confidence { get; init; }
    public required bool NoConfidentAnswer { get; init; }
}

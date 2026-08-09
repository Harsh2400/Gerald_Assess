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
    public required string Snippet { get; init; }
    public required double Score { get; init; }
}

public class AskResponse
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public required List<Citation> Citations { get; init; }
    public required double Confidence { get; init; }
    public required bool NoConfidentAnswer { get; init; }
}

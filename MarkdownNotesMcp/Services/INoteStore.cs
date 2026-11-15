namespace MarkdownNotesMcp.Services;

/// <summary>
/// メモの列挙・読み込み・検索。実装を差し替えれば FTS5 などに移行できる。
/// </summary>
public interface INoteStore
{
    IReadOnlyList<NoteSummary> List(string? folder, DateTime? since);

    IReadOnlyList<SearchHit> Search(string query, int limit);

    NoteContent Read(string path);
}

public sealed record NoteSummary(string Path, string Title, DateTime Modified);

public sealed record SearchHit(string Path, string Title, DateTime Modified, int Score, IReadOnlyList<string> Snippets);

public sealed record NoteContent(string Path, string Title, DateTime Modified, string Content, bool Truncated);

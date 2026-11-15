using System.ComponentModel;
using MarkdownNotesMcp.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MarkdownNotesMcp.Tools;

/// <summary>
/// Claude から呼び出すツール群（すべて読み取り専用）。
/// </summary>
[McpServerToolType]
public sealed class NoteTools(INoteStore store)
{
    [McpServerTool(Name = "search_notes", ReadOnly = true)]
    [Description("""
        Searches the user's personal Markdown notes by keyword (case-insensitive substring match).
        Multiple space-separated words are combined with AND.
        Returns matching notes with path, title, last modified time, and short snippets around matches, NOT the full text.
        Call read_note with a returned path to get the full content of the notes you actually need.
        Notes are mostly written in Japanese; try both Japanese and English keywords if results are sparse.
        """)]
    public IReadOnlyList<SearchHit> SearchNotes(
        [Description("Keywords to search for. Space-separated words are ANDed.")] string query,
        [Description("Maximum number of notes to return (1-50).")] int limit = 10)
        => Run(() => store.Search(query, limit));

    [McpServerTool(Name = "read_note", ReadOnly = true)]
    [Description("""
        Reads the full content of one Markdown note.
        The path must be relative to the notes root, exactly as returned by search_notes or list_notes (e.g. "dotnet/async-await.md").
        Very long notes are truncated, in which case 'truncated' is true.
        """)]
    public NoteContent ReadNote(
        [Description("Note path relative to the notes root, e.g. \"dotnet/async-await.md\".")] string path)
        => Run(() => store.Read(path));

    [McpServerTool(Name = "list_notes", ReadOnly = true)]
    [Description("""
        Lists Markdown notes (path, title, last modified time), newest first.
        Use this to browse which notes exist, optionally narrowed to a subfolder or to notes modified since a date.
        """)]
    public IReadOnlyList<NoteSummary> ListNotes(
        [Description("Optional subfolder relative to the notes root, e.g. \"azure\". Omit for all notes.")] string? folder = null,
        [Description("Optional date (yyyy-MM-dd). Only notes modified on or after this date are returned.")] DateTime? since = null)
        => Run(() => store.List(folder, since));

    // SDK は通常の例外メッセージを隠すため、入力エラーは McpException に包んで理由を Claude に伝える
    private static T Run<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (ArgumentException ex)
        {
            throw new McpException(ex.Message, ex);
        }
    }
}

using System.Text;

namespace MarkdownNotesMcp.Services;

/// <summary>
/// インデックスを持たず、呼び出しのたびに全ファイルを走査する実装（数百ファイル規模向け）。
/// </summary>
public sealed class FileScanNoteStore(PathGuard guard) : INoteStore
{
    private const int MaxContentChars = 20_000;
    private const int MaxSnippetsPerNote = 3;
    private const int SnippetContextLines = 1;

    public IReadOnlyList<NoteSummary> List(string? folder, DateTime? since)
    {
        var dir = guard.Resolve(folder);
        if (!Directory.Exists(dir))
        {
            throw new ArgumentException($"フォルダが存在しません: {folder}");
        }

        return EnumerateNotes(dir)
            .Where(f => since is null || f.LastWriteTime >= since)
            .Select(f => new NoteSummary(guard.ToRelative(f.FullName), ReadTitle(f), f.LastWriteTime))
            .OrderByDescending(n => n.Modified)
            .ToList();
    }

    public IReadOnlyList<SearchHit> Search(string query, int limit)
    {
        // 空白（全角含む）区切りで複数語 AND 検索
        var terms = query.Split([' ', '　', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            throw new ArgumentException("検索語を指定してください。");
        }

        var hits = new List<SearchHit>();
        foreach (var file in EnumerateNotes(guard.Root))
        {
            var text = ReadText(file.FullName);
            if (!terms.All(t => text.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var lines = text.Split('\n');
            hits.Add(new SearchHit(
                guard.ToRelative(file.FullName),
                ExtractTitle(lines, file),
                file.LastWriteTime,
                terms.Sum(t => CountOccurrences(text, t)),
                BuildSnippets(lines, terms)));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenByDescending(h => h.Modified)
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();
    }

    public NoteContent Read(string path)
    {
        var full = guard.Resolve(path);
        if (!full.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Markdown（.md）ファイルのみ読み込めます。");
        }

        // シンボリックリンク等はルート外を指し得るので読まない
        var file = new FileInfo(full);
        if (!file.Exists || file.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new ArgumentException($"メモが見つかりません: {path}");
        }

        var text = ReadText(full);
        var truncated = text.Length > MaxContentChars;
        if (truncated)
        {
            text = text[..MaxContentChars];
        }

        return new NoteContent(guard.ToRelative(full), ExtractTitle(text.Split('\n'), file), file.LastWriteTime, text, truncated);
    }

    // ルート配下の .md を列挙する。隠し・システム・リパースポイントは除外
    private IEnumerable<FileInfo> EnumerateNotes(string dir)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
        };

        return new DirectoryInfo(dir)
            .EnumerateFiles("*.md", options)
            .Where(f => guard.IsUnderRoot(f.FullName));
    }

    private static string ReadText(string fullPath) => File.ReadAllText(fullPath, Encoding.UTF8).Replace("\r\n", "\n");

    // タイトルは先頭付近にあるはずなので、一覧では全文を読まず先頭数十行だけ見る
    private static string ReadTitle(FileInfo file)
        => ExtractTitle(File.ReadLines(file.FullName, Encoding.UTF8).Take(30), file);

    // 最初の「# 見出し」をタイトルとする。無ければファイル名
    private static string ExtractTitle(IEnumerable<string> lines, FileInfo file)
    {
        var heading = lines.Select(l => l.TrimEnd('\r')).FirstOrDefault(l => l.StartsWith("# "));
        return heading is not null ? heading[2..].Trim() : Path.GetFileNameWithoutExtension(file.Name);
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += term.Length;
        }
        return count;
    }

    // マッチした行の前後数行をスニペットとして返す（重なる範囲はまとめる）
    private static List<string> BuildSnippets(string[] lines, string[] terms)
    {
        var snippets = new List<string>();
        var lastEnd = -1;
        for (var i = 0; i < lines.Length && snippets.Count < MaxSnippetsPerNote; i++)
        {
            if (i <= lastEnd || !terms.Any(t => lines[i].Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var start = Math.Max(Math.Max(0, i - SnippetContextLines), lastEnd + 1);
            var end = Math.Min(lines.Length - 1, i + SnippetContextLines);
            snippets.Add($"L{start + 1}: " + string.Join("\n", lines[start..(end + 1)]).Trim());
            lastEnd = end;
        }
        return snippets;
    }
}

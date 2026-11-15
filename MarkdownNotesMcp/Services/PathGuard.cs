namespace MarkdownNotesMcp.Services;

/// <summary>
/// ルートフォルダ外へのアクセスを防ぐ。パスはすべてここを通して解決する。
/// </summary>
public sealed class PathGuard
{
    public string Root { get; }

    public PathGuard(string root)
    {
        // 末尾の区切り文字を揃えて前方一致判定の誤検出（C:\notes と C:\notes2 など）を防ぐ
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    /// <summary>
    /// ルート相対パスをフルパスに解決する。ルート外を指す場合は例外。
    /// </summary>
    public string Resolve(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Root;
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("絶対パスは指定できません。ルートからの相対パスを指定してください。");
        }

        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        if (!IsUnderRoot(full))
        {
            throw new ArgumentException("ルートフォルダ外のパスは指定できません。");
        }

        return full;
    }

    public bool IsUnderRoot(string fullPath)
    {
        return string.Equals(fullPath, Root, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// フルパスをルート相対（区切りは / ）に変換する。
    /// </summary>
    public string ToRelative(string fullPath)
    {
        return Path.GetRelativePath(Root, fullPath).Replace('\\', '/');
    }
}

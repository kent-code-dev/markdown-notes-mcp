using System.Diagnostics;
using MarkdownNotesMcp.Services;

// 使い方: dotnet run -c Release --project Benchmark -- <ルート1> <ルート2> ...
// ルートは相対パスで渡す（出力にはこの文字列をそのまま使い、フルパスは出さない）
string[] roots = args.Length > 0 ? args : ["sample-notes", "../dotnet-docs"];
string[] queries = ["async", "dependency injection", "HttpClient"];
const int WarmRuns = 5;
const int Limit = 10; // search_notes の既定値と同じ

Console.WriteLine("| 対象 | .md 数 | 合計サイズ | 操作 | ヒット数 | 1回目 (ms) | 2回目以降 平均 (ms) |");
Console.WriteLine("|---|---:|---:|---|---:|---:|---:|");

foreach (var root in roots)
{
    if (!Directory.Exists(root))
    {
        Console.Error.WriteLine($"フォルダが存在しません: {root}");
        continue;
    }

    var guard = new PathGuard(root);
    var store = new FileScanNoteStore(guard);

    // ファイル数と合計サイズ（ストアと同じ列挙条件）
    var files = new DirectoryInfo(guard.Root).EnumerateFiles("*.md", new EnumerationOptions
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        IgnoreInaccessible = true,
    }).ToList();
    var count = files.Count;
    var size = FormatSize(files.Sum(f => f.Length));

    // list_notes（フォルダ・日付指定なし）
    var (listCold, listWarm, listCount) = Measure(() => store.List(null, null).Count);
    Console.WriteLine($"| {root} | {count:N0} | {size} | list_notes | {listCount:N0} | {listCold:F1} | {listWarm:F1} |");

    // search_notes
    foreach (var q in queries)
    {
        var (cold, warm, hits) = Measure(() => store.Search(q, Limit).Count);
        Console.WriteLine($"| {root} | {count:N0} | {size} | search \"{q}\" | {hits} | {cold:F1} | {warm:F1} |");
    }
}

// 1回目（コールド）と、続く WarmRuns 回の平均を計測する
static (double Cold, double WarmAvg, int Result) Measure(Func<int> action)
{
    var sw = Stopwatch.StartNew();
    var result = action();
    var cold = sw.Elapsed.TotalMilliseconds;

    double total = 0;
    for (var i = 0; i < WarmRuns; i++)
    {
        sw.Restart();
        action();
        total += sw.Elapsed.TotalMilliseconds;
    }
    return (cold, total / WarmRuns, result);
}

static string FormatSize(long bytes) => bytes >= 1024 * 1024
    ? $"{bytes / 1024.0 / 1024.0:F1} MB"
    : $"{bytes / 1024.0:F1} KB";

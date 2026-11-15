using System.Text.Encodings.Web;
using System.Text.Json;
using MarkdownNotesMcp.Services;
using ModelContextProtocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdio トランスポートでは stdout が JSON-RPC の通信路になるため、ログはすべて stderr に出す
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// メモのルートフォルダ。相対パスはカレントディレクトリ（Claude Code の起動ディレクトリ）基準で解決する
var notesRoot = Environment.GetEnvironmentVariable("NOTES_ROOT");
if (string.IsNullOrWhiteSpace(notesRoot))
{
    Console.Error.WriteLine("環境変数 NOTES_ROOT が設定されていません。");
    return 1;
}

var rootFullPath = Path.GetFullPath(notesRoot);
if (!Directory.Exists(rootFullPath))
{
    Console.Error.WriteLine($"NOTES_ROOT のフォルダが存在しません: {rootFullPath}");
    return 1;
}

builder.Services.AddSingleton(new PathGuard(rootFullPath));
builder.Services.AddSingleton<INoteStore, FileScanNoteStore>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    // 日本語を \uXXXX にエスケープするとトークンを浪費するため、そのまま出力する
    // （SDK 既定の設定をコピーして Encoder だけ差し替える）
    .WithToolsFromAssembly(serializerOptions: new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

await builder.Build().RunAsync();
return 0;

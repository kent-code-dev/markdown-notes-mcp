# notes-mcp

ローカルの Markdown メモを Claude Code から検索・閲覧するための MCP サーバ（C# / .NET）です。

解説記事: [Zenn の記事](https://zenn.dev/kent_code/articles/79e4f6903e00b5)

## 概要

- 指定したフォルダ配下の `*.md` を対象に、キーワード検索・本文の読み込み・一覧表示ができます
- **読み取り専用**です。メモの作成・編集・削除はできません
- 要約などは Claude 側で行います。サーバは「探す・読む」だけを担当します
- 検索はインデックスを持たない全件スキャンです（大文字小文字を区別しない部分一致、空白区切りの複数語は AND）
- 公式 C# MCP SDK（NuGet: `ModelContextProtocol`）を使った stdio サーバです

### 提供するツール

| ツール | 引数 | 返すもの |
|---|---|---|
| `search_notes` | `query`, `limit`（既定 10、最大 50） | パス・タイトル・更新日時・マッチ箇所のスニペット（本文全体は返さない） |
| `read_note` | `path`（ルートからの相対パス） | 本文（20,000 文字を超える場合は切り詰め） |
| `list_notes` | `folder?`, `since?`（yyyy-MM-dd） | パス・タイトル・更新日時の一覧（新しい順） |

`search_notes` で候補を絞り、必要なメモだけ `read_note` で読む 2 段構えにすることで、Claude のコンテキストを節約します。

### 安全対策

- ルートフォルダ外のパス（`../` や絶対パス）は拒否します
- シンボリックリンクなどのリパースポイント、隠しファイル・システムファイルは対象外です
- 読み込めるのは `.md` ファイルだけです

## 必要なもの

- .NET 10 SDK 以上
- Claude Code

## 使い方

### 1. .mcp.json で登録する

リポジトリ直下の `.mcp.json` にサーバを記述します（project スコープ）。パスはすべて相対パスで書けるので、ユーザー名入りの絶対パスをファイルに残さずに済みます。

```json
{
  "mcpServers": {
    "notes": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "MarkdownNotesMcp", "--no-launch-profile", "--verbosity", "quiet"],
      "env": {
        "NOTES_ROOT": "sample-notes"
      }
    }
  }
}
```

| 項目 | 説明 |
|---|---|
| `args` の `--project` | サーバのプロジェクトフォルダ |
| `--verbosity quiet` | ビルドの出力を抑えます。stdio では標準出力が通信路なので、余計な出力があると接続が壊れます |
| `env.NOTES_ROOT` | 検索対象のメモフォルダ。相対パスは Claude Code を起動したディレクトリ基準で解決されます |

### 2. Claude Code を起動して承認する

```sh
cd notes-mcp
claude
```

- 初回は project スコープの MCP サーバを使うかどうかの承認ダイアログが出るので、許可します
- `/mcp` で接続状態とツール一覧を確認できます
- 「Azure のコストに関するメモを要約して」のように頼むと、Claude がツールを使って検索・要約します

### 別のメモフォルダを追加する

同じプロジェクトを、`NOTES_ROOT` を変えて複数登録できます。

```json
"dotnet-docs": {
  "type": "stdio",
  "command": "dotnet",
  "args": ["run", "--project", "MarkdownNotesMcp", "--no-build", "--no-launch-profile", "--verbosity", "quiet"],
  "env": {
    "NOTES_ROOT": "../dotnet-docs"
  }
}
```

2 つ目以降には `--no-build` を付けます。複数のサーバが同時にビルドを始めて衝突するのを避けるためです（1 つ目のサーバがビルドを担当します）。

### 注意点

- **`claude` は必ずリポジトリ直下で起動してください。** サブフォルダで起動すると相対パスがずれて、サーバが起動しません
- `dotnet run` で起動するため、最初の接続はビルド分の数秒がかかります
- 全件スキャンなので、数百ファイル程度なら一瞬ですが、1 万ファイルを超えると 1 回あたり 1 秒近くかかります（計測結果: [docs/notes/benchmark.md](docs/notes/benchmark.md)）

## 構成

```
MarkdownNotesMcp/
  Program.cs                     // ホストの構築、stdio トランスポートの登録
  Tools/NoteTools.cs             // MCP ツール 3 本
  Services/INoteStore.cs         // 検索・読み込みのインターフェース（差し替え可能）
  Services/FileScanNoteStore.cs  // 全件スキャンの実装
  Services/PathGuard.cs          // ルート外アクセスの防止
Benchmark/                       // FileScanNoteStore の速度計測
sample-notes/                    // 動作確認用のサンプルメモ
docs/notes/                      // 設計ノート
```

## ライセンス

[MIT License](LICENSE)

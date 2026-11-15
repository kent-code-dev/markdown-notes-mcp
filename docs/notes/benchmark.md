# FileScanNoteStore ベンチマーク

## 論点一覧

| 状態 | 論点 |
|---|---|
| 確認済み | 現行の全走査実装の速度（下表） |
| 未確認 | dotnet-docs 規模（約1.4万ファイル）で 1回あたり約0.8〜0.9秒を許容するか |
| 未確認 | 高速化するならどの方式にするか（インデックス／FTS5／キャッシュなど） |

## 結論

- sample-notes（16ファイル）はどの操作も 1ms 前後で、まったく問題ない
- dotnet-docs（13,985ファイル・57.5MB）は list_notes も search_notes も **1回あたり約0.8〜0.9秒**かかる
- 2回目以降もほとんど速くならない。毎回全ファイルを読む設計なので、時間はファイル数と総サイズに比例する

## 結果

計測日: 2026-11-15 / Release ビルド / .NET SDK 11.0.100 / Windows 11

| 対象 | .md 数 | 合計サイズ | 操作 | ヒット数 | 1回目 (ms) | 2回目以降 平均 (ms) |
|---|---:|---:|---|---:|---:|---:|
| sample-notes | 16 | 6.0 KB | list_notes | 16 | 25.9 | 0.9 |
| sample-notes | 16 | 6.0 KB | search "async" | 1 | 3.2 | 0.7 |
| sample-notes | 16 | 6.0 KB | search "dependency injection" | 0 | 0.8 | 1.0 |
| sample-notes | 16 | 6.0 KB | search "HttpClient" | 0 | 1.7 | 0.8 |
| ../dotnet-docs | 13,985 | 57.5 MB | list_notes | 13,985 | 949.5 | 761.1 |
| ../dotnet-docs | 13,985 | 57.5 MB | search "async" | 10 | 907.9 | 867.7 |
| ../dotnet-docs | 13,985 | 57.5 MB | search "dependency injection" | 10 | 818.0 | 839.0 |
| ../dotnet-docs | 13,985 | 57.5 MB | search "HttpClient" | 10 | 838.8 | 840.5 |

## 計測条件と注意

- 2回目以降の平均は 5 回分
- 検索の上限件数は既定値と同じ 10 件。ヒット数 10 は「上限まで返った」という意味
- 「1回目」はプロセス内で最初の呼び出しのこと。JIT の時間は含むが、OS のファイルキャッシュは消していない
  - dotnet-docs は直前に MCP 経由で検索していたので、キャッシュが効いている可能性が高い。本当のコールド（再起動直後など）ではもっと遅くなるはず
- sample-notes の list_notes の 1回目（25.9ms）は、ほとんどが JIT・初期化の時間
- dotnet-docs の list_notes は先頭 30 行しか読まないが、ファイルを開く回数が多いので search とほぼ同じ時間になる

## 再実行方法

```bash
# notes-mcp フォルダで実行
dotnet run -c Release --project Benchmark -- sample-notes ../dotnet-docs
```

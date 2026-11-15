# HttpClient の正しい使い方（dotnet-docs 調査まとめ）

## 論点一覧

| 状態 | 論点 |
|---|---|
| 未確認 | 自分のアプリではどちらの戦略（長寿命 static / IHttpClientFactory）を採るか |
| 未確認 | Cookie を使う必要があるか（使うなら Factory は非推奨） |
| 未確認 | 同時リクエスト数の上限（MaxConnectionsPerServer）を設けるか |
| 確認済み | 公式推奨の2戦略と、それぞれの注意点（本ノートに整理） |

## 結論

- `HttpClient` を **リクエストごとに new / Dispose しない**（ポート枯渇の原因）
- 選ぶ戦略は2つのどちらか
  1. **長寿命**: `static` / シングルトンの `HttpClient` ＋ `SocketsHttpHandler.PooledConnectionLifetime` を設定（DNS 変更に追従）
  2. **短寿命**: `IHttpClientFactory` で都度作成（推奨は Typed client）。作った client は長く保持しない
- .NET Framework では `IHttpClientFactory` を使う

## 1. なぜ使い回しが必要か

- `HttpClient` はハンドラ（`SocketsHttpHandler`）ごとに接続プールを持つ
- 毎回作り直すと新規接続が発生し、閉じた TCP ポートは `TIME-WAIT` ですぐ解放されない → 高負荷時に**ポート枯渇**
- 一方、使い回しすぎると **DNS 変更を拾わない**（接続作成時にしか名前解決せず、TTL も見ない）

→ この2つの問題を同時に解くのが下記2戦略。

## 2. 戦略A: 長寿命クライアント + PooledConnectionLifetime

```csharp
// アプリ全体で共有する HttpClient
private static readonly HttpClient sharedClient = new(new SocketsHttpHandler
{
    // 接続を定期的に作り直して DNS 変更に追従する（値は DNS 変更頻度に合わせる）
    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
});
```

- Factory のオーバーヘッドなしでポート枯渇・DNS 問題を両方解決
- インスタンスは「少数」なら複数でも可（プロキシ別、Cookie コンテナ分離など）
- リトライ等は `Microsoft.Extensions.Http.Resilience` の `ResilienceHandler` でハンドラを包めば static でも使える

## 3. 戦略B: IHttpClientFactory（DI 前提）

- パッケージ: `Microsoft.Extensions.Http`
- 使い方: Basic / Named / **Typed（推奨）** / Generated（Refit など）
- Factory はハンドラをプールし、既定 **2分**（`SetHandlerLifetime` で変更可）で再生成 → DNS 追従
- Factory が作った `HttpClient` は **Dispose しても安全**（ハンドラは破棄されない）

```csharp
// Typed client の登録
services.AddHttpClient<TodoService>(c =>
{
    c.BaseAddress = new Uri("https://example.com/");
});
```

### Factory + SocketsHttpHandler の併用

```csharp
services.AddHttpClient("name")
    .UseSocketsHttpHandler((handler, _) =>
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2)) // 接続の再生成はハンドラ側で
    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);                  // Factory 側のローテーションは無効化
```

## 4. よくある落とし穴（Troubleshooting より）

| ❌ やりがち | 問題 | ✔️ 対策 |
|---|---|---|
| Factory 製 `HttpClient` / Typed client を Singleton に注入・長期キャッシュ | DNS 変更を拾わない | 必要なときに都度 Factory から取得 |
| Typed client を `AddTransient` などで別途登録 | 設定が消え、素の `HttpClient` が注入される | `AddHttpClient<T>` だけで登録 |
| 複数 Typed client を同一インターフェースに名前指定なしで登録 | 同じ named client が共有される | 名前を明示する |
| ハンドラ内で `HttpContext` 等スコープ情報をキャッシュ | ハンドラは独自 DI スコープ。情報漏えいの恐れ | `HttpRequestMessage.Options` で渡す／Factory 外の `DelegatingHandler` で包む |
| Factory で Cookie を使う | `CookieContainer` が共有・ローテーションで消失 | Cookie が要るなら戦略Aを使う |
| HTTP/1.1 で大量の同時リクエストを無制限に投げる | 接続が際限なく増えソケット枯渇 | `MaxConnectionsPerServer` を設定／HTTP/2 を使う |
| named client 名を外部入力から動的に生成 | リソース枯渇 | 名前は有限・固定に |

## 5. 補足（.NET 9 の変更）

- Factory の既定プライマリハンドラが `SocketsHttpHandler` に変更（`HttpClientHandler` へのキャストは不可に）
- Factory のログはヘッダ値・URI クエリを既定で伏字化

## 参照（dotnet-docs）

- docs/fundamentals/networking/http/httpclient-guidelines.md
- docs/core/extensions/httpclient-factory.md
- docs/core/extensions/httpclient-factory-troubleshooting.md
- docs/core/compatibility/networking/9.0/default-handler.md

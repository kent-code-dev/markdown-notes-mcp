# ログ設計

- 構造化ログ：メッセージテンプレートに {OrderId} のように名前付きで埋め込む
- ログレベルの目安：Information は業務イベント、Debug は開発時のみ、Warning は自動回復できた異常
- 例外は ex を第1引数に渡す。メッセージに ex.Message を連結しない
- stdout をプロトコルに使うアプリ（MCP の stdio など）ではログを stderr に出す

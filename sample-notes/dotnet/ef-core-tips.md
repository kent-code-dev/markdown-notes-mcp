# EF Core メモ

- AsNoTracking は読み取り専用のクエリで使う。追跡しない分だけ速い
- N+1 問題：ループの中でナビゲーションプロパティを触らない。Include で先に読む
- マイグレーションは本番適用前に SQL を出力してレビューする（dotnet ef migrations script）
- 大量更新は ExecuteUpdate / ExecuteDelete を使う

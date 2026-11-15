# DI のライフタイム

- Singleton：アプリ全体で1つ。状態を持つならスレッドセーフにする
- Scoped：リクエストごとに1つ。DbContext はここ
- Transient：毎回生成。軽いステートレスなサービス向け
- Singleton から Scoped を注入すると、Scoped が実質 Singleton になってしまう（キャプチャ依存）

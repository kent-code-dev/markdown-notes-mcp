# async/await の落とし穴

- ConfigureAwait(false) はライブラリ側で使う。UI アプリのイベントハンドラでは不要
- async void はイベントハンドラ以外で使わない。例外が拾えない
- .Result や .Wait() はデッドロックの原因になる。同期コンテキストがある環境では特に注意
- Task.WhenAll を await すると、投げられるのは最初の1件の例外だけ。すべての例外を見たいときは、WhenAll の Task を変数に取って .Exception（AggregateException）を見る

# Nullable 参照型

- 新規プロジェクトでは <Nullable>enable</Nullable> を最初から有効にする
- ! 演算子（null 免除）は最後の手段。使うならコメントで理由を書く
- 外部入力（JSON、DB）は null が来る前提で受ける
- required 修飾子でコンストラクタを増やさずに必須プロパティを表現できる

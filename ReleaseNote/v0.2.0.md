# Release Note v0.2.0

## 新機能
- ゲームのコアとなる掘削とアイテム収集のサイクルを実装しました。
- Unityの新しいInput Systemに対応しました。

## 変更点
- 特になし。

## その他
- 特になし。

## スクリプト実装概要

### `PlayerController.cs`
- Unityの新しいInput Systemを利用したプレイヤーの移動制御（水平・垂直モード）
- 掘削範囲(`Digger`)の動的な更新
- `DroppedItem`との衝突によるスコア加算機能

### `Digger.cs`
- `BoxCollider`を利用した掘削範囲の定義
- マウスクリックをトリガーとした掘削実行 (`Dig()`)
- `Physics.OverlapBox`による効率的なチャンク検出

### `VoxelChunk.cs`
- ボクセルデータ（種類、HP）の3次元配列管理
- 隠面消去を実装した動的メッシュ生成
- 指定範囲のボクセルを破壊し、アイテムをドロップする機能

### `BaseCubePlacer.cs`
- `VoxelChunk`をシーンに配置・初期化するための抽象基底クラス
- URP用マテリアルの動的生成と設定

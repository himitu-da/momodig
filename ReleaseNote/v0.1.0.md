# Release Note v0.1.0

## 新機能
- プレイヤーの移動と穴掘り機能のプロトタイプを実装
- アイテムドロップ機能のプロトタイプを実装
- 縦横方向の生成ギズモを追加

## 変更点
- `.gitignore` をUnityプロジェクト用に更新
- ゲームプランの初期バージョンを追加

## その他
- プロジェクトの初期セットアップ

## スクリプト実装概要

### `PlayerController.cs`
- UnityのInput Systemを利用してプレイヤーの移動を制御します。
- `Vertical`（垂直）と`Horizonal`（水平）の2つの移動モードをサポートし、モードに応じて移動平面を切り替えます。
- 移動方向に応じてプレイヤーの向きを自動的に調整します。
- ドロップされたアイテム（"DroppedItem"タグ）を回収し、スコアを管理する機能を持ちます。

### `Digger.cs`
- プレイヤーの掘削機能を担当します。
- `OverlapBox`を使い、指定された範囲内の"Block"タグを持つオブジェクトを検出して破壊します。
- ブロックを破壊した位置にアイテムをドロップ（生成）します。
- 掘削範囲を視覚的に確認するためのデバッグ用のワイヤーフレームを描画します。

### `BasePlayerGenerator.cs`
- プレイヤーオブジェクトを生成するための抽象基底クラスです。
- プレイヤーの初期位置、掘削範囲のサイズやオフセットを設定できます。
- `PlayerController`や`Digger`など、プレイヤーに必要なコンポーネントを動的にアタッチしてプレイヤーを生成します。
- 移動モード（`Vertical`または`Horizonal`）を派生クラスで定義することを強制します。

### `SphereHorizonalGenerator.cs` & `SphereVerticalGenerator.cs`
- `BasePlayerGenerator`を継承したクラスです。
- それぞれ`Horizonal`モードと`Vertical`モードのプレイヤーを生成します。

### `CubeHorizonalPlacer.cs` & `CubeVerticalPlacer.cs`
- 指定された範囲にキューブを配置するスクリプトです。
- `HorizonalPlacer`はXZ平面に、`VerticalPlacer`はXY平面にキューブを配置します。
- 配置されたキューブには"Block"タグが設定され、市松模様に色分けされます。

### `DroppedItem.cs`
- 掘削によってドロップされたアイテムの挙動を制御します。
- アイテムがその場で回転するシンプルなアニメーションを実装しています。

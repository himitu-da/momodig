# TerrainManager テストガイド

## テスト目的
`TerrainManager`が既存の`CubeSideScrollerPlacer`と同等の結果を生成することを手動で検証する

## テスト環境セットアップ

### 1. 新規テストシーンの準備
1. 新しいシーンを作成（名前：`TerrainManager_Test`）
2. 基本的なライティング設定を適用

### 2. 既存システム（比較対象）のセットアップ
1. 空のGameObject作成（名前：`Original_CubeSideScroller`）
2. `CubeSideScrollerPlacer`コンポーネントをアタッチ
3. 以下の設定を適用：

```
Center: (0, -5, 0)
Chunk Count: (20, 3)
Chunk Size: 1
Texture1: [テクスチャを指定]
Texture2: [テクスチャを指定]
Voxel Hp: 2
Voxel Size: 4
Dropped Item Prefab: [Voxelプリファブを指定]
Disable Rotation: true
Auto Scale: true
Scale Multiplier: 1.0
```

### 3. 新システム（テスト対象）のセットアップ
1. 空のGameObject作成（名前：`WorldGenerator`）
2. `TerrainManager`コンポーネントをアタッチ
3. **全く同じ設定値を適用**：

```
Basic Settings:
  Center: (0, -5, 0)
  Chunk Count: (20, 3)
  Chunk Size: 1
  Voxel Size: 4
  Voxel Hp: 2

Texture Settings:
  Texture1: [同じテクスチャ]
  Texture2: [同じテクスチャ]

Dropped Item Settings:
  Dropped Item Prefab: [同じVoxelプリファブ]
  Disable Rotation: true
  Auto Scale: true
  Scale Multiplier: 1.0

Generation Type:
  Generation Type: SideScroller
```

## 検証項目

### 1. 生成される地形の形状
- [ ] チャンク数が同じ（20x3）
- [ ] 各チャンクの位置が同じ
- [ ] 各チャンクのサイズが同じ
- [ ] Z軸制限（0.5以下）が正しく適用されている

### 2. ボクセル構造
- [ ] 各チャンク内のボクセル配置が同一
- [ ] ボクセルの数が同一
- [ ] 隠面消去が正しく動作している

### 3. マテリアル・テクスチャ
- [ ] 同じテクスチャが適用されている
- [ ] URP Transparentマテリアルが使用されている
- [ ] テクスチャのUVマッピングが同一

### 4. 掘削機能
- [ ] プレイヤーでの掘削動作が同一
- [ ] ドロップアイテムの生成が同一
- [ ] アイテムのスケール・回転設定が同一

### 5. パフォーマンス
- [ ] メッシュ生成時間がほぼ同等
- [ ] メモリ使用量がほぼ同等
- [ ] フレームレートに違いがない

## 具体的テスト手順

### ステップ1: 基本生成テスト
1. 両方のGameObjectを異なるX座標に配置（例：Original=X:-15, New=X:+15）
2. 実行ボタンを押す
3. 生成された地形を視覚的に比較

### ステップ2: 詳細比較テスト
1. Scene Viewで両方の地形を上から見る
2. チャンクの配置とサイズを比較
3. 個別チャンクのボクセル数を確認（DebugInfoをtrueにして）

### ステップ3: 機能テスト
1. プレイヤーを配置
2. 両方の地形で掘削テスト
3. ドロップアイテムの動作を比較

### ステップ4: エッジケーステスト
1. 極端な設定値での動作確認
   - ChunkCount: (1,1), (50,10)
   - VoxelSize: 1, 16
   - ChunkSize: 0.1, 10.0

## 合格基準

- 生成される地形が視覚的に同一
- チャンク数、位置、サイズが数値的に同一
- 掘削機能が同一動作
- パフォーマンスの差が10%以内

## 問題が発生した場合の対処

### 地形の形状が異なる場合
1. `TerrainManager`の`GenerateSideScrollerPattern()`メソッドを確認
2. Z軸制限の計算ロジックを`CubeSideScrollerPlacer`と比較

### マテリアル設定が異なる場合
1. `CreateChunk()`メソッドのマテリアル設定部分を確認
2. URP Shader設定を確認

### ドロップアイテムが異なる場合
1. `VoxelChunk.Initialize()`の呼び出しパラメータを確認
2. 既存システムとの引数順序を確認

## デバッグ支援機能

### TerrainManagerのデバッグ機能
- `Show Debug Info`をtrueにすると生成ログが出力される
- `Regenerate Terrain`右クリックメニューで再生成可能
- エディタでの値変更時に自動検証機能

### 比較用カウンタ機能
各システムで以下を確認可能：
- 生成されたチャンク数
- 各チャンクのアクティブボクセル数
- 総ボクセル数

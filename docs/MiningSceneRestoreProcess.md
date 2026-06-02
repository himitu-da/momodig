# MiningScene Restore Process

## 目的

MiningScene の復元は、Persistence データから採掘場の状態を戻しながら、プレイヤーが未復元地形へ落ちたり、Manager が独自タイミングで復元を始めたりしないように順序を固定する。

復元の入口は `MiningSceneRestoreCoordinator` とし、Scene / Inspector で明示された参照を正とする。参照不足や不正データは自動探索や補完ではなく `Debug.LogError` で fail-fast する。

## フェーズ

現在の Coordinator フェーズは次の順序で進む。

1. `Validate`
   Persistence データと必須 Scene 参照を検証する。不正な blockDataName、範囲外 voxel、null record、未設定参照などはエラーにする。

2. `TerrainBaseline`
   terrain seed を確定し、TerrainManager 初期化前に地形基準状態を利用可能にする。

3. `TerrainInitialization`
   TerrainManager / ChunkManager が初期チャンク生成へ進める状態にする。

4. `ChunkRestore`
   chunk 生成完了通知を受け取り、chunk 単位で松明とドロップアイテムを復元する。

5. `PlayerRestore`
   初期 chunk 群の復元完了後、player 系の復元完了フェーズとして記録する。

6. `PostRestore`
   3x3 復元完了時に開始済みのランタイム処理があることを確認する。ここで追加の一括スナップや半径5完了待ちは行わない。

7. `Completed`
   初期 chunk 群である 3x3 の復元が完了し、通常のチャンク生成ライフサイクルへ移行できる状態。

## プレイヤー操作ロック

プレイヤー操作は、MiningScene 起動時と初期 chunk 復元開始時に Coordinator からロックする。

ロック対象:

- player input
- player item pickup

同時に、次のシステムも復元中として停止する。

- minecart movement
- fairy AI
- fluid simulation

## プレイヤー操作解除条件

プレイヤー操作の解除は、初期 chunk 群全体の完了までは待たない。

解除条件は、プレイヤーが位置する chunk と隣接 chunk の合計 3x3 chunk が復元済みになること。

具体的には、`PlayerChunkPosition` を中心に次の 9 chunk を必須とする。

- `(x - 1, y - 1)` から `(x + 1, y + 1)` まで
- z は `PlayerChunkPosition.z` と同じ

この 9 chunk が `NotifyChunkRestored` で全て restored になった時点で、Coordinator は次を開始する。

- camera snap / camera presentation restore
- light source dirty
- chunk 単位の terrain brightness refresh
- dropped item brightness / fluid tick candidate refresh
- fluid active cell queue
- fluid simulation resume
- `playerController.SetItemPickupLocked(false)`
- `playerController.SetControlLocked(false)`

これにより、プレイヤーは周辺 3x3 の地形・松明・ドロップ復元が終わり、カメラ・明るさ・液体演算が動き始めた時点で操作可能になる。

## プレイヤー周辺の復元

プレイヤー自身の所属 chunk が restored になった時点で、次を先に実行する。

- player motion reset
- minecart path reset
- fairy home reset

camera snap と scene presentation restore は操作解除より早く実行しない。必ず 3x3 chunk restored 完了を待つ。

## 初期 chunk 群と半径5の関係

初期 chunk 群はプレイヤーを中心とする 3x3 chunk とする。

プレイヤーを含む半径5 chunk は、初期復元の完了条件には含めない。これは通常ゲーム進行中に生成される新規地形エリアと同じ扱いで、ChunkManager の生成キューに残ったまま並列して生成を継続する。

3x3 完了後に半径5内の追加 chunk が restored になった場合、その chunk は特別な後処理完了待ちをせず、通常の chunk 完了通知として次に参加する。

- persisted torch loading
- persisted dropped item loading
- light source dirty
- chunk 単位の terrain brightness refresh
- chunk 範囲内の active fluid cell queue

半径5全体の生成完了は、プレイヤー操作解除・カメラ開始・明るさ開始・液体演算開始の条件にしない。

## PostRestore 再計算

3x3 復元完了時に、保存しない派生状態をランタイム処理へ参加させる。

対象:

- light source dirty
- chunk 単位の terrain brightness refresh
- dropped item brightness refresh
- dropped item fluid tick candidate refresh
- fluid active cells dirty queue

この処理は 3x3 の完了時点で開始し、その後に復元された chunk は chunk ごとに同じ通常ライフサイクルへ参加する。半径5全体の生成完了を待って一括再計算する形にはしない。

明るさ計算と表示反映の詳細は `docs/MiningBrightnessSystem.md` にまとめる。

流体システムの責務分割、Prefab 設定、fail-fast 方針は `docs/FluidSystem.md` にまとめる。

## 現在移動済みの復元責務

Coordinator 配下へ寄せているもの:

- terrain seed / terrain baseline
- restore data validation
- chunk generated / chunk restored tracking
- persisted dropped item loading
- persisted torch loading
- fluid simulation pause / resume
- player gameplay unlock timing
- chunk runtime activation

まだ専用 persistence record がないもの:

- player position persistence
- minecart persistence
- fairy persistence
- fluid persistence data record

これらを追加する場合も、復元入口は Coordinator に置き、Inspector / Scene / 明示 API で依存を渡す。

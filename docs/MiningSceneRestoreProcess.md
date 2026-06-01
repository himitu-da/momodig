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
   保存しない派生状態をまとめて再計算する。

7. `Completed`
   初期 chunk 群全体の復元が完了し、minecart / fairy / fluid を再開できる状態。

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

この 9 chunk が `NotifyChunkRestored` で全て restored になった時点で、Coordinator は次を実行する。

- `playerController.SetItemPickupLocked(false)`
- `playerController.SetControlLocked(false)`

これにより、プレイヤーは周辺 3x3 の地形・松明・ドロップ復元が終わった時点で操作可能になる。

## プレイヤー周辺の復元

プレイヤー自身の所属 chunk が restored になった時点で、次を先に実行する。

- player motion reset
- minecart path reset
- fairy home reset
- camera snap

これは操作解除より早く起きる可能性がある。操作解除は必ず 3x3 chunk restored 完了を待つ。

## 初期 chunk 群完了後に再開するもの

次の処理は、プレイヤー操作解除より後でもよく、初期 chunk 群全体の復元完了まで待つ。

- minecart movement resume
- fairy AI resume
- fluid simulation resume
- `Completed` への遷移

理由は、これらが動き出すと周辺外の未復元状態や派生計算に影響しやすいためである。

## PostRestore 再計算

初期 chunk 群全体の復元完了時に、保存しない派生状態をまとめて再計算または再キューする。

対象:

- light source dirty
- terrain brightness refresh
- dropped item brightness refresh
- dropped item fluid tick candidate refresh
- fluid active cells dirty queue
- camera snap

この処理は、各 Manager が起動直後に独自タイミングで過剰に再計算することを減らし、Profiler で復元コストを追いやすくするために Coordinator からまとめて呼ぶ。

## 現在移動済みの復元責務

Coordinator 配下へ寄せているもの:

- terrain seed / terrain baseline
- restore data validation
- chunk generated / chunk restored tracking
- persisted dropped item loading
- persisted torch loading
- fluid simulation pause / resume
- player gameplay unlock timing
- post-restore recalculation

まだ専用 persistence record がないもの:

- player position persistence
- minecart persistence
- fairy persistence
- fluid persistence data record

これらを追加する場合も、復元入口は Coordinator に置き、Inspector / Scene / 明示 API で依存を渡す。

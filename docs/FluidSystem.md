# FluidSystem

## 目的

FluidSystem は MiningScene 内の流体グリッド、流体シミュレーション、描画メッシュ、飛沫、プレイヤー/ドロップアイテムの水中抵抗を扱う。

Scene / Inspector で明示された `FluidManager`、`TerrainManager`、`FluidDefinition`、`FluidSplash` Prefab を正とし、設定不足をコード側で自動生成・自動補完しない。

## 責務分割

- `FluidManager`
  - 外部公開 API の入口。
  - `AddFluidAtWorldPosition`、`GetFluidFillRatioAtWorldPosition`、`QueueExplosion`、restore 用 pause/resume などを保持する。
  - MonoBehaviour として Scene 参照、Inspector 設定、version 更新を管理する。

- `FluidManager.Simulation`
  - tick 内のセル処理、速度移動、重力移動、横移動、リットル転送、注入 BFS を扱う。
  - `FluidSimulationSolver` は tick 入口として `FluidManager.StepSimulationCore` を呼び出す。

- `FluidManager.TerrainQuery`
  - 流体セルが地形・動的障害物で塞がれているかを判定する。
  - `TerrainManager`、`BlockGenerator`、`GameDataPersistenceManager` への依存はここへ寄せる。

- `FluidManager.GridAndDirection`
  - 重力方向、横方向、内部セル/描画セルの補助処理を扱う。

- `FluidManager.Types`
  - `FluidCellState`、`LateralCandidate`、`FluidImpulse` など内部状態型を保持する。

- `FluidMeshRenderer`
  - 描画更新タイミング、Mesh / Material / Renderer 設定だけを扱う。

- `FluidRenderMeshBuilder`
  - 流体セル snapshot の描画セル集約、面生成、mesh buffer 構築を扱う。

- `FluidSubmersionSampler`
  - Bounds 内の流体充填率サンプリングを共通化する。
  - `PlayerController` と `DroppedItem` の水中抵抗計算から利用する。

## 設定不備の扱い

- `FluidSource.fluidManager` が未設定の場合は `Debug.LogError` を出し、注入しない。
- `FluidSplash.Initialize` は `FluidManager` と `FluidDefinition` が必須。未設定時は代替色・代替密度を使わず停止する。
- `FluidManager.fluidSplashPrefab` は root に `FluidSplash` component を持つ Prefab を割り当てる。足りない場合に `AddComponent<FluidSplash>()` で補完しない。
- `FluidMeshRenderer` は override material がない場合に `Custom/FluidUnlit` を使う。Shader が見つからない場合は `Debug.LogError` で知らせる。

## Restore との関係

MiningScene 復元中は `MiningSceneRestoreCoordinator` が `FluidManager.PauseSimulationForRestore` / `ResumeSimulationAfterRestore` を呼び、流体 tick を停止・再開する。

復元済み chunk が runtime に参加したときは、chunk 範囲内の active fluid cell を dirty queue に戻す。流体 persistence record はまだないため、保存される派生状態ではなく runtime 再計算対象として扱う。

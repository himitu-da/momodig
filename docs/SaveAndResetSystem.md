# Save And Reset System

## 目的

セーブ機能は `GameDataPersistenceManager` のランタイム状態を JSON ファイルとして永続化する。
簡易リセットは保存済み JSON / 一時ファイル / バックアップを削除し、同時に現在の `GameDataPersistenceManager` のランタイム状態を初期化する。

## 保存場所

保存先は `Application.persistentDataPath/momodig_save_v1.json` とする。

同じディレクトリに次の補助ファイルを使う。

- `momodig_save_v1.json.tmp`
- `momodig_save_v1.json.bak`

保存時は一時ファイルへ UTF-8 no BOM で書き出し、既存ファイルがある場合は `File.Replace` で置き換える。

## 保存タイミング

`GameDataPersistenceManager` が次のタイミングで保存する。

- 起動時: 保存ファイルがあれば `Awake` でロードする
- 実行中: 地上 / 地下 Scene のセーブボタンを押したときに保存する

保存対象が完全に空の場合は、空の保存ファイルを新規作成せず、既存の保存ファイルを削除状態に保つ。
これにより、タイトルで簡易リセットした直後にセーブボタンを押さない限り空ファイルを再生成しない。

## 保存対象

現時点の保存対象は `GameDataPersistenceManager` が保持している次のデータとする。

- terrain seed
- destroyed blocks
- partially destroyed voxel positions
- stored resources
- dropped items
- voxel cell overrides
- solidified voxel history
- facility upgrade progress
- owned tool ids
- tool inventory slot bindings
- torch placements
- mining lighting cache

`Dictionary` / `HashSet` / `MiningTool` 参照は直接 JSON 化しない。
保存専用 DTO へ変換してから `JsonUtility` でシリアライズする。

`mining lighting cache` はロード高速化用の派生データであり、ゲーム状態の正規データではない。
`MiningLightManager` は cache version / terrain state hash / 光源 signature が一致した場合だけ利用し、一致しない場合は通常の光計算に戻る。
保存対象は恒久光源のみとし、一時光源、burst light、移動する光源は保存しない。

## ツール参照

`MiningTool` はファイル上では `toolId` として保存する。
現在の `toolId` は `MiningTool.name` を使う。

ロード直後の `GameDataPersistenceManager` では `MiningTool` 参照は null のまま保持し、MiningScene 起動時に `ToolInventory` が Inspector で明示されたツール一覧から `toolId` を解決する。
解決できない場合は `Debug.LogError` を出す。

`owned tool ids` は `StorageManager` が管理する所有ツールの正本である。
`tool inventory slot bindings` / `mainToolSlotId` / `subToolSlotId` は、地下へ持ち込むツールと左右クリック割り当ての設定として扱う。
地下 runtime の `ToolInventory` は保存済み設定を読み込むが、地下での一時的な並べ替えは保存へ書き戻さない。

## 簡易リセット

タイトル側のボタンは `Title_Button_System.SelectResetSaveKey()` を呼ぶ。

このメソッドは `GameDataPersistenceManager.Instance.DeleteSaveAndResetRuntimeState()` を呼び、保存ファイル削除とメモリ上の Persistence 初期化を同時に行う。

## 未対応の保存対象

次のデータはまだ専用 persistence record がないため、現時点ではディスク保存対象に含めない。

- player position
- minecart state
- fairy state
- fluid persistence data

これらを追加する場合も、復元入口は `MiningSceneRestoreCoordinator` に置き、Scene / Inspector / 明示 API で依存を渡す。

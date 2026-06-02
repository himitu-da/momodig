# Mining Brightness System

## 目的

MiningScene の明るさは、光源から voxel cell へ伝播した結果を `MiningLightManager` が保持し、`MiningTerrainBrightnessApplier` が Block の vertex color に反映する。

このシステムでは、光が届いていない cell を `0f` として常時保持しない。光が届く cell だけが `MiningLightManager` の `composedBrightness` に登録される。

## 主要コンポーネント

### MiningLightManager

`MiningLightManager` は光源の登録、伝播計算、cell ごとの brightness 合成を担当する。

主な責務:

- `MiningLightSource` の登録と解除
- 光源変更時の propagation restart
- 地形変更時の terrain repair propagation
- `composedBrightness` の更新
- 表示反映が必要な cell を dirty brightness cell として queue する

伝播計算のフレーム上限は `maxPropagationCellsPerFrame` で管理する。MiningScene では現在 `432`。

### MiningTerrainBrightnessApplier

`MiningTerrainBrightnessApplier` は `MiningLightManager` の brightness を Block mesh の vertex color に反映する。

反映処理のフレーム上限は `maxBrightnessCellsPerFrame` で管理する。MiningScene では現在 `512`。

この値は伝播計算の上限ではなく、mesh color 反映の上限である。大きくすると反映は速くなるが、`Block.ApplyBrightness` が mesh color を更新するため、上げすぎるとフレームスパイクの原因になり得る。

## dirty brightness cell

dirty brightness cell は、`MiningLightManager` 内で brightness が更新され、見た目への反映が必要になった cell である。

流れ:

1. propagation が cell の brightness を更新する。
2. `composedBrightness` が更新される。
3. `dirtyBrightnessCells` に cell が入る。
4. `MiningTerrainBrightnessApplier` が Block へ反映する。

dirty brightness cell は「伝播未完了 cell」ではない。LightManager 側の brightness 更新が発生した後の「描画反映待ち cell」である。

dirty brightness cell 経路では、`TryGetBrightness == false` を `0f` として反映する。これは、以前は明るかった cell から brightness entry が削除された場合に、見た目を正しく暗く戻すためである。

## block refresh

block refresh は、Block 内の brightness 対象 cell をまとめて現在の LightManager 状態で塗り直す補助処理である。

発生条件:

- `MiningTerrainBrightnessApplier.OnEnable()` で既存 active block を queue する。
- `TerrainCellsChanged` を受けたとき、変更 cell を含む block を queue する。
- chunk restored 後、Coordinator が chunk 内の block を queue する。

block refresh は cell 単位の brightness 更新通知ではない。chunk や地形変更に伴う広めの再適用である。

block refresh 経路では、`TryGetBrightness == false` の cell を `0f` で上書きしない。光が届いていない場合と、まだ propagation が届いていない場合を区別できないためである。新規 block は生成時点で暗く初期化されるので、値がない cell を skip しても通常は暗いまま維持される。

## 反映優先度

`MiningTerrainBrightnessApplier` は dirty brightness cell を block refresh より先に処理する。

理由:

- dirty brightness cell は実際に brightness が変化した cell である。
- block refresh は補助的な再適用であり、復元中や chunk 生成中に大量に発生しやすい。
- プレイヤー周囲の見た目を早く更新するには、dirty brightness cell を優先する必要がある。

この優先度により、半径5 chunk の生成が続いている間も、3x3 復元完了後に伝播済みとなったプレイヤー周囲の brightness が先に表示へ反映される。

## 復元時の流れ

3x3 chunk restored 完了時:

1. Coordinator が `MiningLightManager.MarkLightSourcesDirty()` を呼ぶ。
2. LightManager が次の Update で propagation を開始または再開する。
3. propagation が進むたびに dirty brightness cell が発生する。
4. Applier が dirty brightness cell を優先して Block へ反映する。
5. 半径5内の追加 chunk は restored ごとに block refresh queue へ入るが、dirty brightness cell の反映を先に通す。

半径5全体の生成完了は、brightness propagation や brightness 反映開始の条件にしない。

## lighting cache persistence

MiningScene の明るさはロード高速化のために `GameDataPersistenceManager` へ cache として保存する。

この cache は正規データではない。正規データは terrain seed、破壊済み block / voxel override、固化 voxel、松明配置、光源 profile、光計算アルゴリズムであり、brightness cache はそれらから導かれる派生結果である。

保存対象は `MiningLightSource.IncludeInLightingCache == true` の恒久光源だけとする。松明は配置時に cache 対象として構成される。一時光源、burst light、移動する光源は保存対象にしない。

cache には terrain state hash と cache version を持たせる。ロード時は現在の persistence 状態から再計算した terrain state hash と一致する場合だけ使用する。光源ごとの cache は source cell と profile signature が一致する場合だけ復元する。

cache hit した光源は `MiningLightManager` の source display brightness を復元し、FullSource propagation を省略する。復元した brightness は既存の dirty brightness cell 経路で Block へ反映する。

復元中に一部 chunk が未ロードでも、未ロード chunk の cache record を即時削除しない。cache から hydrate しただけの状態では保存 cache を再発行せず、実際の propagation 結果が落ち着いたタイミングでのみ cache を更新する。


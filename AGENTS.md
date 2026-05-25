# AGENTS.md

## 基本方針
- このプロジェクトでは、Unity上に表示されるInspectorやHierarchyの状態やSceneでの配置を正とする
  - HierarchyやInspectorで数値や範囲を調整しやすいことが重要です

- このプロジェクトでは、フォールバック処理／自動探索／自動生成は行わない
  - このプロジェクトで禁止する fallback とは、本来 Inspector・Scene・Prefab・明示 API で設定されているべき依存関係や状態が欠けているときに、コード側で自動探索・自動生成・代替値・黙殺によって処理を継続することを指す
  - 禁止するフォールバック処理の例を示す
    - FindFirstObjectByType<T>()、FindObjectOfType<T>()、GameObject.Find()、GameObject.FindWithTag() などの探索系 API をコード上で使用する
    - new GameObject(...) で Manager を自動生成する
    - AddComponent<T>() で不足 Component を自動追加する
    - Scene にない Manager を static getter で自動生成する
    - 例外や設定不備を握りつぶして default 値で進める
    - 参照が null のときに「とりあえず処理をスキップ」して正常扱いする
    - missing script / missing reference を放置したまま別処理で辻褄を合わせる
  - これらは技術的負債やパフォーマンス低下を生むリスクがあるためである

- 全てのフォールバックが悪いわけではなく、一部のフォールバックは明示的な仕様として許容する
  - Editor direct play 時に OverWorldScene / MiningScene から BaseScene に誘導する
  - GameDataPersistenceManager.Instance を Scene 横断データの正式入口として使う（唯一のDDOLとして許可している）
  - 値が未設定・不正な場合に Debug.LogError を出して処理を止める（これは fallback ではなく fail-fast）
  - TryGetComponent を「同じ GameObject 上の任意 Component 確認」に使う（依存の自動探索ではなく、対象が明確なら許容可能。ただし必須依存なら serialized field 推奨）

- このプロジェクトでは、上記のように正しくない設定の場合は積極的にエラー・ログを出す
  - 本ゲームは非常に規模が大きくなるので、つじつま合わせや後先を考えない実装は後々の保守性を著しく損なう
  - 短期的な実装のために、長期的な保守性を犠牲にしない
  - 後先を考えて修正しやすい状態にする

- Profilerを積極的に活用するため、ProfilerMarkerをある程度配置することが重要です

## ファイルの読み書き
ファイルの読み書きは「UTF-8 no BOM」でしてください。

**Rule:** In each command, **define → use**. Do **not** escape `$`. Use generic `'path/to/file.ext'`.

### 1) READ (UTF-8 no BOM, line-numbered)

```bash
bash -lc 'powershell -NoLogo -Command "
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false);
Set-Location -LiteralPath (Convert-Path .);
function Get-Lines { param([string]$Path,[int]$Skip=0,[int]$First=40)
  $enc=[Text.UTF8Encoding]::new($false)
  $text=[IO.File]::ReadAllText($Path,$enc)
  if($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF){ $text=$text.Substring(1) }
  $ls=$text -split "`r?`n"
  for($i=$Skip; $i -lt [Math]::Min($Skip+$First,$ls.Length); $i++){ "{0:D4}: {1}" -f ($i+1), $ls[$i] }
}
Get-Lines -Path "path/to/file.ext" -First 120 -Skip 0
"'
```

### 2) WRITE (UTF-8 no BOM, atomic replace, backup)

```bash
bash -lc 'powershell -NoLogo -Command "
$OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false);
Set-Location -LiteralPath (Convert-Path .);
function Write-Utf8NoBom { param([string]$Path,[string]$Content)
  $dir = Split-Path -Parent $Path
  if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
  }
  $tmp = [IO.Path]::GetTempFileName()
  try {
    $enc = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($tmp,$Content,$enc)
    Move-Item $tmp $Path -Force
  }
  finally {
    if (Test-Path $tmp) {
      Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    }
  }
}
$file = "path/to/your_file.ext"
$enc  = [Text.UTF8Encoding]::new($false)
$old  = (Test-Path $file) ? ([IO.File]::ReadAllText($file,$enc)) : ''
Write-Utf8NoBom -Path $file -Content ($old+"`nYOUR_TEXT_HERE`n")
"'
```

## Sceneライフタイム
- DontDestroyOnLoadはGameDataPersistenceManagerのみ許可する。Scene横断データの正式入口として使うためである
- Scene横断を許容しているManagerは、BaseSceneに配置する。GameSceneCoordinator、FrameRateLimiter、EventSystem、GameDataPersistenceManagerである。追加する場合は、その都度検討を行う
- 各シーン固有のマネージャーは、そのシーンのルート GameObject に配置する。例えばOverWorldSceneのFacilityUpgradeService、MiningSceneのTerrainManagerなどである

## 依存解決のルール
- 禁止するAPI
  - FindFirstObjectByType<T>()
  - FindObjectOfType<T>()
  - GameObject.Find()
  - FindAnyObjectOfType<T>()
  - FindAnyObjectByType<T>()
- 原則
  - [SerializeField] private T dependency; で Inspector から依存関係を設定する
  - SceneのローカルのManager系はSceneに配置する
  - 依存関係が不足している場合は、Debug.LogErrorを出して処理を止める（fail-fast）

## Scene YAML 編集ルール
- .unity ファイルを直接編集して GameObject / Component / Transform を削除した場合は、SceneRoots.m_Roots に削除済み Transform の fileID が残っていないか確認する。
- 削除した GameObject / Component / Transform の fileID が Scene 内に残っていないか rg で確認する。
- m_Script: {fileID: 0} を残さない。
- Broken text PPtr が出た場合は、削除済み fileID の残存参照を最優先で調査する。

## RenderQueue

When changing or adding world-space rendering, use `Assets/Utility/RenderQueue.cs` as the source of truth for draw order.

| Layer | Queue |
| --- | ---: |
| Background | 4000 |
| Scenery | 4001 |
| Geometry | 4100 |
| Minecart | 4101 |
| Player | 4200 |
| PlayerTool | 4201 |
| Foreground | 4250 |
| WorldSpaceUI | 4300 |
| ScreenSpaceUI | 5000 |

Notes:

- Larger queue values draw later.
- Screen-space Overlay UI is ordered by Canvas hierarchy, not this table.
- Prefer an existing `RenderQueue` layer plus an offset over hard-coded queue numbers.
- Use `RenderQueueApplier` when scene renderers need per-instance queue overrides. It clones `sharedMaterials` at runtime, applies the queue, restores originals on destroy, and destroys runtime materials.
- Runtime-generated materials should set `renderQueue` explicitly when draw order matters. Current examples include fluid surfaces at `RenderQueue.Geometry + 50 + renderQueueOffset` and dropped item outlines at `RenderQueue.Geometry + 50`.
- If `FluidMeshRenderer.overrideMaterial` is assigned, that material's own RenderQueue is used; the default `Geometry + 50` runtime queue is not applied.

## Codex Sandbox Note

On this Windows environment, sandboxed shell startup may fail with:

`windows sandbox: timed out after 15000ms connecting runner pipe-in`

If this happens for read-only investigation commands such as `Get-ChildItem`, `rg`, `git status`, or file reads, retry the same read-only command with escalation. Do not use this as a reason to escalate write, delete, network, install, or Unity/Editor-launch commands without separate justification.

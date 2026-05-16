# AGENTS.md

## 実装方針
- コード上で自動的に解決はしないかつ、フォールバック処理は絶対にせず、エラーやログに出る形とする
- UnityはあくまでGUI上での操作を前提として、コード上での自動的な解決、オブジェクト探索、自動的なフォールバックは技術的な負債を生じる
- 正規の手段以外は積極的にエラーやログに出る形にする。本ゲームは非常に規模が大きくなるので、つじつま合わせや後先を考えない実装は後々の保守性を著しく損なう
- 短期的な実装のために、長期的な保守性を犠牲にしないこと。短期的な実装のために、コード上での自動的な解決、オブジェクト探索、自動的なフォールバックを行わないこと
- 正規の手段以外は積極的にエラーやログに出る形にして、後先を考えて修正しやすい状態にする
- HierarchyやInspectorで数値や範囲を調整しやすいことが重要です


**Rule:** In each command, **define → use**. Do **not** escape `$`. Use generic `'path/to/file.ext'`.

## 1) READ (UTF-8 no BOM, line-numbered)

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

## 2) WRITE (UTF-8 no BOM, atomic replace, backup)

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

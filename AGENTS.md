# AGENTS.md

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

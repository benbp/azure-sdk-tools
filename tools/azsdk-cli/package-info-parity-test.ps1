param(
  [Parameter(Mandatory = $true)]
  [string]$RepoRoot,

  [Parameter(Mandatory = $true)]
  [string]$CliPath,

  [string]$ServiceDirectory = "",

  [switch]$FromDiff,

  [string]$TargetPath = "",

  [string[]]$ExcludePaths = @()
)

$repoRootFull = Resolve-Path $RepoRoot
$scriptDir = Join-Path $repoRootFull "eng" "common" "scripts"
$saveScript = Join-Path $scriptDir "Save-Package-Properties.ps1"
$diffScript = Join-Path $scriptDir "Generate-PR-Diff.ps1"

$psOutDir = Join-Path $repoRootFull "artifacts" "PackageInfo-ps"
$cliOutDir = Join-Path $repoRootFull "artifacts" "PackageInfo-cli"
$diffDir = Join-Path $repoRootFull "artifacts" "diff"

Remove-Item -Recurse -Force $psOutDir, $cliOutDir, $diffDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $psOutDir | Out-Null
New-Item -ItemType Directory -Force $cliOutDir | Out-Null

if ($FromDiff) {
  $targetPathValue = $TargetPath
  if ([string]::IsNullOrEmpty($targetPathValue)) {
    $targetPathValue = $repoRootFull
  }

  & $diffScript -TargetPath $targetPathValue -ArtifactPath $diffDir -ExcludePaths $ExcludePaths
  if ($LASTEXITCODE -ne 0) {
    throw "Generate-PR-Diff.ps1 failed with exit code $LASTEXITCODE"
  }

  $diffJson = Join-Path $diffDir "diff.json"
  & $saveScript -PrDiff $diffJson -OutDirectory $psOutDir
}
else {
  & $saveScript -ServiceDirectory $ServiceDirectory -OutDirectory $psOutDir
}

if ($LASTEXITCODE -ne 0) {
  throw "Save-Package-Properties.ps1 failed with exit code $LASTEXITCODE"
}

$cliArgs = @("eng", "package-info", "--out-dir", $cliOutDir, "--repo-root", $repoRootFull)
if ($FromDiff) {
  $cliArgs += "--from-diff"
  if (-not [string]::IsNullOrEmpty($TargetPath)) {
    $cliArgs += @("--target-path", $TargetPath)
  }
  foreach ($path in $ExcludePaths) {
    $cliArgs += @("--exclude-path", $path)
  }
}
elseif (-not [string]::IsNullOrEmpty($ServiceDirectory)) {
  $cliArgs += @("--service-directory", $ServiceDirectory)
}

& $CliPath @cliArgs
if ($LASTEXITCODE -ne 0) {
  throw "azsdk eng package-info failed with exit code $LASTEXITCODE"
}

function ConvertTo-OrderedObject {
  param([object]$InputObject)
  if ($null -eq $InputObject) {
    return $null
  }

  if ($InputObject -is [System.Collections.IDictionary]) {
    $ordered = [ordered]@{}
    foreach ($key in ($InputObject.Keys | Sort-Object)) {
      $ordered[$key] = ConvertTo-OrderedObject $InputObject[$key]
    }
    return $ordered
  }

  if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
    $items = @()
    foreach ($item in $InputObject) {
      $items += ConvertTo-OrderedObject $item
    }
    return $items
  }

  if ($InputObject -is [PSCustomObject]) {
    $ordered = [ordered]@{}
    foreach ($property in ($InputObject.PSObject.Properties | Sort-Object Name)) {
      $ordered[$property.Name] = ConvertTo-OrderedObject $property.Value
    }
    return $ordered
  }

  return $InputObject
}

function Normalize-Json {
  param([string]$Path)
  $raw = Get-Content -Raw $Path
  $obj = $raw | ConvertFrom-Json -Depth 100
  $ordered = ConvertTo-OrderedObject $obj
  return ($ordered | ConvertTo-Json -Depth 100 -Compress)
}

$psFiles = Get-ChildItem -Recurse -Filter *.json $psOutDir
$cliFiles = Get-ChildItem -Recurse -Filter *.json $cliOutDir

$psRelative = $psFiles | ForEach-Object { $_.FullName.Substring($psOutDir.Length).TrimStart('\', '/') } | Sort-Object
$cliRelative = $cliFiles | ForEach-Object { $_.FullName.Substring($cliOutDir.Length).TrimStart('\', '/') } | Sort-Object

$diff = Compare-Object $psRelative $cliRelative
if ($diff) {
  Write-Host "File list mismatch:"
  $diff | ForEach-Object { Write-Host "  $($_.InputObject) ($($_.SideIndicator))" }
  throw "PackageInfo file list mismatch"
}

$failed = $false
foreach ($relativePath in $psRelative) {
  $psFile = Join-Path $psOutDir $relativePath
  $cliFile = Join-Path $cliOutDir $relativePath

  $psNormalized = Normalize-Json $psFile
  $cliNormalized = Normalize-Json $cliFile

  if ($psNormalized -ne $cliNormalized) {
    Write-Host "Content mismatch for $relativePath"
    $failed = $true
  }
}

if ($failed) {
  throw "PackageInfo content mismatches detected"
}

Write-Host "PackageInfo parity check passed."

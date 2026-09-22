param([Parameter(Mandatory = $true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$assets = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'src\QingLan\obj\project.assets.json') | ConvertFrom-Json
$notices = Join-Path $OutputDirectory 'licenses'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
$inventory = @()
foreach ($entry in $assets.libraries.PSObject.Properties) {
    if ($entry.Value.type -ne 'package') { continue }
    $packageRoot = $null
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder $entry.Value.path
        if (Test-Path -LiteralPath $candidate) { $packageRoot = [IO.Path]::GetFullPath($candidate); break }
    }
    if (-not $packageRoot) { throw "Cannot find restored package: $($entry.Name)" }
    $specFile = Get-ChildItem -LiteralPath $packageRoot -Filter '*.nuspec' -File | Select-Object -First 1
    if (-not $specFile) { throw "Missing package metadata: $($entry.Name)" }
    [xml]$spec = Get-Content -Raw -LiteralPath $specFile.FullName
    $metadata = $spec.package.metadata
    $license = $metadata.license
    $files = @(Get-ChildItem -LiteralPath $packageRoot -File | Where-Object { $_.Name -match '^(licen[cs]e|copying|notice|third[-_. ]?party)' })
    if ($license -and $license.type -eq 'file') {
        $declared = [IO.Path]::GetFullPath((Join-Path $packageRoot $license.InnerText))
        if (-not $declared.StartsWith($packageRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'License path leaves package directory.' }
        $files += Get-Item -LiteralPath $declared
    }
    $copied = @()
    foreach ($file in $files | Sort-Object FullName -Unique) {
        $relative = $file.FullName.Substring($packageRoot.Length + 1)
        $target = Join-Path (Join-Path $notices $entry.Value.path) $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        $copied += ($entry.Value.path + '/' + $relative.Replace('\', '/'))
    }
    $inventory += [ordered]@{
        package = $entry.Name
        licenseType = if ($license) { [string]$license.type } else { '' }
        license = if ($license) { [string]$license.InnerText } else { '' }
        licenseUrl = [string]$metadata.licenseUrl
        projectUrl = [string]$metadata.projectUrl
        bundledNotices = $copied
    }
}
$inventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $notices 'nuget-packages.json') -Encoding UTF8
Write-Output "Bundled notices and license metadata for $($inventory.Count) restored NuGet packages."

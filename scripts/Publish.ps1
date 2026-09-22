$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & dotnet publish 'src\QingLan\QingLan.csproj' -c Release -p:Platform=x64 -o 'artifacts\publish'
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $output = Join-Path $repoRoot 'artifacts\publish'
    foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $output -Force
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs') -Destination $output -Recurse -Force
    & (Join-Path $PSScriptRoot 'Export-DependencyNotices.ps1') -OutputDirectory $output
    Write-Output 'Ready: artifacts\publish\QingLan.exe. Share the entire publish folder.'
}
finally { Pop-Location }

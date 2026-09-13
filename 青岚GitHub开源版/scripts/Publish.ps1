$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    & dotnet publish 'src\QingLan\QingLan.csproj' -c Release -p:Platform=x64 -o 'artifacts\publish'
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Write-Output 'Ready: artifacts\publish\QingLan.exe. Share the entire publish folder.'
}
finally { Pop-Location }

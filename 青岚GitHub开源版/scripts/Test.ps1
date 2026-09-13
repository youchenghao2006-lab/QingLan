$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$builtinPath = Join-Path $repoRoot 'src\QingLan\Assets\builtins.json'
Push-Location $repoRoot
try {
    & dotnet run --project 'tests\QingLan.Tests\QingLan.Tests.csproj' -c Release -- $builtinPath
    if ($LASTEXITCODE -ne 0) { throw 'Reading tests failed.' }
    & dotnet run --project 'tests\QingLan.CampusTests\QingLan.CampusTests.csproj' -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Campus tests failed.' }
}
finally { Pop-Location }

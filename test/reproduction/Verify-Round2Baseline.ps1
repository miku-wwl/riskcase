param([string]$Results = (Join-Path $PSScriptRoot '../results/round3-independent-baseline'))
$ErrorActionPreference = 'Stop'
if (-not $env:ConnectionStrings__RiskCaseDb) { throw 'Set ConnectionStrings__RiskCaseDb to a disposable PostgreSQL database (CREATE DATABASE privilege required).' }
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$scratchRoot = [IO.Path]::GetFullPath($env:TEMP)
$scratch = Join-Path $scratchRoot ('riskcase-baseline-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$resultsPath = [IO.Path]::GetFullPath($Results)
New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null
try {
    $archive = Join-Path $scratch 'baseline.zip'
    git -C $repository archive --format=zip --output=$archive 4667f194e4847e06a0764d40f0bc78be7420541c
    if ($LASTEXITCODE -ne 0) { throw 'git archive failed' }
    $source = Join-Path $scratch 'source'
    Expand-Archive -LiteralPath $archive -DestinationPath $source
    $target = Join-Path $source 'test/integration/RiskCase.IntegrationTests'
    Copy-Item -LiteralPath (Join-Path $repository 'test/integration/RiskCase.IntegrationTests/RiskCaseApiFactory.cs') -Destination $target
    Copy-Item -LiteralPath (Join-Path $repository 'test/integration/RiskCase.IntegrationTests/PresenterReadProbe.cs') -Destination $target
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BaselineUpgradeTests.cs.txt') -Destination (Join-Path $target 'BaselineUpgradeTests.cs')
    dotnet test (Join-Path $target 'IntegrationTests.csproj') -c Release --filter FullyQualifiedName~Baseline_F8 --results-directory $resultsPath --logger trx
    if ($LASTEXITCODE -eq 0) { throw 'Unexpected PASS: the pinned baseline should fail independence assertions. Inspect the TRX.' }
    Write-Host 'Expected failing assertions. Verify both INDEPENDENT_SQL observations and actual HTTP=200 in the TRX before treating this as a reproduction.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($scratch)
    if (-not $resolved.StartsWith($scratchRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'riskcase-baseline-*') { throw 'Unsafe cleanup target' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

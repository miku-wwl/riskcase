param(
    [ValidateSet('Baseline','Current')][string]$Edition = 'Current',
    [Parameter(Mandatory)][string]$Output
)
$ErrorActionPreference = 'Stop'
if (-not $env:ConnectionStrings__RiskCaseDb) { throw 'Use a dedicated disposable PostgreSQL database, never a developer/production database.' }
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$scratchRoot = [IO.Path]::GetFullPath($env:TEMP)
$scratch = Join-Path $scratchRoot ('riskcase-measure-' + [guid]::NewGuid().ToString('N'))
$priorOutput = $env:RISKCASE_BENCHMARK_OUTPUT
$env:RISKCASE_BENCHMARK_OUTPUT = [IO.Path]::GetFullPath($Output)
try {
    $source = $repository
    if ($Edition -eq 'Baseline') {
        New-Item -ItemType Directory -Path $scratch | Out-Null
        $archive = Join-Path $scratch 'baseline.zip'
        git -C $repository archive --format=zip --output=$archive 4667f194e4847e06a0764d40f0bc78be7420541c
        if ($LASTEXITCODE -ne 0) { throw 'git archive failed' }
        $source = Join-Path $scratch 'source'
        Expand-Archive -LiteralPath $archive -DestinationPath $source
        $target = Join-Path $source 'test/integration/RiskCase.IntegrationTests'
        foreach ($name in @('RiskCaseApiFactory.cs','PresenterReadProbe.cs','HardeningTests.cs','CaseDetailBenchmark.cs')) {
            Copy-Item -LiteralPath (Join-Path $repository "test/integration/RiskCase.IntegrationTests/$name") -Destination $target
        }
    }
    dotnet test (Join-Path $source 'test/integration/RiskCase.IntegrationTests/IntegrationTests.csproj') -c Release --filter FullyQualifiedName~Measure_case_detail
    if ($LASTEXITCODE -ne 0) { throw 'Measurement failed; inspect output before reporting any metrics.' }
}
finally {
    $env:RISKCASE_BENCHMARK_OUTPUT = $priorOutput
    if (Test-Path -LiteralPath $scratch) {
        $resolved = [IO.Path]::GetFullPath($scratch)
        if (-not $resolved.StartsWith($scratchRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($resolved) -notlike 'riskcase-measure-*') { throw 'Unsafe cleanup target' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

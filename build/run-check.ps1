<#
  Verifie la commande du README : dotnet run ouvre bien la fenetre principale,
  et le logiciel se ferme proprement. S'execute a la racine du depot.
#>
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui-helpers.ps1')

$env:FACTURES_DATA_DIR = Join-Path $env:RUNNER_TEMP ("factures-run-" + [guid]::NewGuid().ToString('N'))
$arguments = 'run --project src/Factures.App -c Release'
$runner = Start-Process -FilePath 'dotnet' -ArgumentList $arguments -PassThru
$app = $null
try {
    # dotnet run compile puis lance FacturesWpf dans un processus enfant.
    Wait-Until {
        Assert-That (-not $runner.HasExited -or $null -ne (Get-Process -Name 'FacturesWpf' -ErrorAction SilentlyContinue)) "dotnet run s'est arrete sans lancer le logiciel"
        $null -ne (Get-Process -Name 'FacturesWpf' -ErrorAction SilentlyContinue)
    } "dotnet run n'a pas lance le logiciel" -TimeoutSeconds 240

    $app = Get-Process -Name 'FacturesWpf' | Select-Object -First 1
    $null = $app.Handle  # conserve le handle : sinon ExitCode reste vide apres la fermeture

    $window = Wait-MainWindow -Process $app
    $null = Wait-Element -Root $window -AutomationId 'dashboard-page'
    Close-MainWindow -Window $window -Process $app
    Assert-That ($runner.WaitForExit(30000)) "dotnet run ne s'est pas termine apres la fermeture"
    Write-Host 'dotnet run verifie.'
}
finally {
    if ($null -ne $app -and -not $app.HasExited) { $app | Stop-Process -Force }
    if (-not $runner.HasExited) { $runner | Stop-Process -Force }
    Remove-Item Env:\FACTURES_DATA_DIR
}

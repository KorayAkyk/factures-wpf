<#
  Verifie le MSI pour de vrai : installation silencieuse, fichiers et raccourci presents,
  lancement du logiciel installe, puis desinstallation complete.
#>
param(
    [Parameter(Mandatory)][string]$MsiPath,
    [Parameter(Mandatory)][string]$LogDir
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui-helpers.ps1')

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$installDir = Join-Path $env:ProgramFiles 'Factures WPF'
$exe = Join-Path $installDir 'FacturesWpf.exe'
$shortcut = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Factures WPF.lnk'

function Invoke-Msi([string]$Action, [string]$LogName) {
    $log = Join-Path $LogDir $LogName
    $arguments = "$Action `"$MsiPath`" /qn /norestart /l*v `"$log`""
    $result = Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Wait -PassThru
    Assert-That ($result.ExitCode -eq 0) "msiexec $Action a renvoye $($result.ExitCode), voir $log"
}

Assert-That (-not (Test-Path $exe)) "Le logiciel est deja installe sur cette machine"

Invoke-Msi '/i' 'installation.log'
Assert-That (Test-Path $exe) "Executable absent apres installation : $exe"
Assert-That (Test-Path $shortcut) "Raccourci du menu Demarrer absent : $shortcut"
Assert-That (Test-Path (Join-Path $installDir 'e_sqlite3.dll')) 'Bibliotheque native SQLite absente'

$env:FACTURES_DATA_DIR = Join-Path $env:RUNNER_TEMP ("factures-msi-" + [guid]::NewGuid().ToString('N'))
$process = Start-Process -FilePath $exe -PassThru
try {
    $window = Wait-MainWindow -Process $process
    $null = Wait-Element -Root $window -AutomationId 'dashboard-page'
    Close-MainWindow -Window $window -Process $process
    Assert-That (Test-Path (Join-Path $env:FACTURES_DATA_DIR 'factures.db')) 'Base SQLite non creee par le logiciel installe'
}
finally {
    if (-not $process.HasExited) { $process | Stop-Process -Force }
}

Invoke-Msi '/x' 'desinstallation.log'
Assert-That (-not (Test-Path $exe)) "Executable encore present apres desinstallation"
Assert-That (-not (Test-Path $shortcut)) "Raccourci encore present apres desinstallation"
Assert-That (Test-Path (Join-Path $env:FACTURES_DATA_DIR 'factures.db')) 'La desinstallation ne doit pas supprimer les donnees'

Remove-Item Env:\FACTURES_DATA_DIR
Write-Host 'Installation, lancement et desinstallation reussis.'

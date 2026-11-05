<#
  Lance le logiciel reel, parcourt ses pages par UI Automation, verifie ce qui s'affiche
  et enregistre une capture de chaque page. Echoue si quelque chose manque.
#>
param(
    [Parameter(Mandatory)][string]$ExePath,
    [string]$CaptureDir
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ui-helpers.ps1')

$dataDir = Join-Path $env:RUNNER_TEMP ("factures-smoke-" + [guid]::NewGuid().ToString('N'))
$env:FACTURES_DATA_DIR = $dataDir

try { Set-DisplayResolution -Width 1600 -Height 1000 -Force -ErrorAction Stop } catch { Write-Host "Resolution inchangee : $_" }

if ($CaptureDir) { New-Item -ItemType Directory -Force -Path $CaptureDir | Out-Null }

$process = Start-Process -FilePath $ExePath -PassThru
try {
    $window = Wait-MainWindow -Process $process

    $null = Wait-Element -Root $window -AutomationId 'dashboard-page'
    $kpi = Wait-Element -Root $window -AutomationId 'kpi-paid'
    Assert-That ($kpi.Current.Name -match '€') "Le montant du tableau de bord n'est pas en euros : '$($kpi.Current.Name)'"
    $null = Wait-Element -Root $window -AutomationId 'overdue-grid'
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'tableau-de-bord.png' })

    Invoke-Element -Root $window -AutomationId 'nav-invoices'
    $grid = Wait-Element -Root $window -AutomationId 'invoices-grid'
    $total = Get-RowCount $grid
    Assert-That ($total -eq 13) "13 factures de demonstration attendues, $total trouvees"
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'factures.png' })

    # La recherche passe par la vraie zone de saisie, jusqu'a la requete LINQ.
    $search = Wait-Element -Root $window -AutomationId 'filter-search'
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('catalogue')
    Wait-Until { (Get-RowCount $grid) -eq 2 } "La recherche 'catalogue' devrait donner 2 factures, $(Get-RowCount $grid) trouvees"
    Invoke-Element -Root $window -AutomationId 'filter-reset'
    Wait-Until { (Get-RowCount $grid) -eq 13 } 'Effacer les filtres devrait redonner 13 factures'

    Invoke-Element -Root $window -AutomationId 'invoice-new'
    $null = Wait-Element -Root $window -AutomationId 'invoice-editor-page'
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'editeur.png' })

    # Un brouillon sans client est refuse, et le message apparait a l'ecran.
    Invoke-Element -Root $window -AutomationId 'editor-save'
    $message = Wait-Element -Root $window -AutomationId 'notification-text'
    Assert-That ($message.Current.Name -eq 'Choisissez un client.') "Message inattendu : '$($message.Current.Name)'"
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'erreur-visible.png' })
    Invoke-Element -Root $window -AutomationId 'editor-cancel'

    Invoke-Element -Root $window -AutomationId 'nav-clients'
    $clients = Wait-Element -Root $window -AutomationId 'clients-grid'
    Assert-That ((Get-RowCount $clients) -eq 5) '5 clients de demonstration attendus'
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'clients.png' })

    Assert-That (-not $process.HasExited) "Le logiciel s'est arrete pendant le parcours"
    Close-MainWindow -Window $window -Process $process
    Write-Host 'Parcours de fumee reussi.'
}
finally {
    if (-not $process.HasExited) { $process | Stop-Process -Force }
    Remove-Item Env:\FACTURES_DATA_DIR
}

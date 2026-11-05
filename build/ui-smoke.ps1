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

$ExePath = (Resolve-Path $ExePath).Path
$dataDir = Join-Path $env:RUNNER_TEMP ("factures-smoke-" + [guid]::NewGuid().ToString('N'))
$env:FACTURES_DATA_DIR = $dataDir

Set-LargestDisplay

if ($CaptureDir) { New-Item -ItemType Directory -Force -Path $CaptureDir | Out-Null }

$process = Start-Process -FilePath $ExePath -PassThru
try {
    $window = Wait-MainWindow -Process $process
    Move-WindowOnScreen -Window $window

    $null = Wait-Element -Root $window -AutomationId 'dashboard-page'
    $kpi = Wait-Element -Root $window -AutomationId 'kpi-paid'
    Wait-Until { $kpi.Current.Name -match [char]0x20AC } "Le montant du tableau de bord n'est pas en euros : '$($kpi.Current.Name)'"
    $null = Wait-Element -Root $window -AutomationId 'overdue-grid'
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'tableau-de-bord.png' })

    Invoke-Element -Root $window -AutomationId 'nav-invoices'
    $grid = Wait-Element -Root $window -AutomationId 'invoices-grid'
    Wait-Until { (Get-RowCount $grid) -eq 13 } '13 factures de demonstration attendues'
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'factures.png' })

    # La recherche passe par la vraie zone de saisie, jusqu'a la requete LINQ.
    $search = Wait-Element -Root $window -AutomationId 'filter-search'
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('catalogue')
    Wait-Until { (Get-RowCount $grid) -eq 2 } "La recherche 'catalogue' devrait donner 2 factures"
    Invoke-Element -Root $window -AutomationId 'filter-reset'
    Wait-Until { (Get-RowCount $grid) -eq 13 } 'Effacer les filtres devrait redonner 13 factures'

    Invoke-Element -Root $window -AutomationId 'invoice-new'
    $null = Wait-Element -Root $window -AutomationId 'invoice-editor-page'
    # Culture francaise : euro dans les totaux, jour/mois/annee dans les dates.
    $editorTotal = Wait-Element -Root $window -AutomationId 'editor-total'
    Assert-That ($editorTotal.Current.Name -match [char]0x20AC) "Total sans euro : '$($editorTotal.Current.Name)'"
    $issueDate = Wait-Element -Root $window -AutomationId 'editor-issue-date'
    $issueText = $issueDate.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value
    Assert-That ($issueText -match '^\d{2}/\d{2}/\d{4}$') "Date hors format francais : '$issueText'"
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'editeur.png' })

    # Un brouillon sans client est refuse, et le message apparait a l'ecran.
    Invoke-Element -Root $window -AutomationId 'editor-save'
    $notification = Wait-Element -Root $window -AutomationId 'notification-text'
    Wait-Until { $notification.Current.Name -eq 'Choisissez un client.' } "Message d'erreur absent : '$($notification.Current.Name)'"
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'erreur-visible.png' })
    Invoke-Element -Root $window -AutomationId 'editor-cancel'

    Invoke-Element -Root $window -AutomationId 'nav-clients'
    $clients = Wait-Element -Root $window -AutomationId 'clients-grid'
    Wait-Until { (Get-RowCount $clients) -eq 5 } '5 clients de demonstration attendus'
    Save-Capture -Window $window -Path $(if ($CaptureDir) { Join-Path $CaptureDir 'clients.png' })

    Assert-That (-not $process.HasExited) "Le logiciel s'est arrete pendant le parcours"
    Close-MainWindow -Window $window -Process $process
    Write-Host 'Parcours de fumee reussi.'
}
finally {
    if (-not $process.HasExited) { $process | Stop-Process -Force }
    Remove-Item Env:\FACTURES_DATA_DIR
}

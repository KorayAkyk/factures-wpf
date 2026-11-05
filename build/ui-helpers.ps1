# Fonctions communes aux verifications de bout en bout (UI Automation et captures).

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class NativeWindow
{
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@
[NativeWindow]::SetProcessDPIAware() | Out-Null

function Assert-That([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "ECHEC : $Message" }
}

function Wait-Until([scriptblock]$Condition, [string]$FailureText, [int]$TimeoutSeconds = 20) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 200
    }
    throw "ECHEC : $FailureText"
}

function Wait-MainWindow($Process, [int]$TimeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ($Process.HasExited) { throw "Le logiciel s'est arrete au demarrage (code $($Process.ExitCode))." }
        $Process.Refresh()
        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) {
            [NativeWindow]::SetForegroundWindow($Process.MainWindowHandle) | Out-Null
            return [System.Windows.Automation.AutomationElement]::FromHandle($Process.MainWindowHandle)
        }
        Start-Sleep -Milliseconds 300
    }
    throw "La fenetre principale n'est pas apparue en $TimeoutSeconds s."
}

function Set-LargestDisplay {
    foreach ($size in @(@(1920, 1080), @(1600, 900), @(1280, 1024))) {
        try {
            Set-DisplayResolution -Width $size[0] -Height $size[1] -Force -ErrorAction Stop | Out-Null
        }
        catch {
            continue
        }
        Start-Sleep -Seconds 2
        $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        if ($screen.Width -ge $size[0]) { break }
    }
    $screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    Write-Host "Ecran : $($screen.Width) x $($screen.Height)"
}

function Move-WindowOnScreen($Window, [int]$Width = 1200, [int]$Height = 780) {
    $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $transform = $Window.GetCurrentPattern([System.Windows.Automation.TransformPattern]::Pattern)
    $transform.Resize([Math]::Min($Width, $area.Width), [Math]::Min($Height, $area.Height))
    $transform.Move($area.X, $area.Y)
}

function Find-Element($Root, [string]$AutomationId) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Wait-Element($Root, [string]$AutomationId, [int]$TimeoutSeconds = 20) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $element = Find-Element -Root $Root -AutomationId $AutomationId
        if ($null -ne $element) { return $element }
        Start-Sleep -Milliseconds 200
    }
    throw "ECHEC : element '$AutomationId' introuvable en $TimeoutSeconds s."
}

function Invoke-Element($Root, [string]$AutomationId) {
    $element = Wait-Element -Root $Root -AutomationId $AutomationId
    $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Get-RowCount($Grid) {
    return $Grid.GetCurrentPattern([System.Windows.Automation.GridPattern]::Pattern).Current.RowCount
}

function Save-Capture($Window, [string]$Path) {
    if (-not $Path) { return }
    Start-Sleep -Milliseconds 700
    $bounds = $Window.Current.BoundingRectangle
    $bitmap = New-Object System.Drawing.Bitmap([int]$bounds.Width, [int]$bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen([int]$bounds.X, [int]$bounds.Y, 0, 0, $bitmap.Size)
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    Write-Host "Capture : $Path ($([int]$bounds.Width) x $([int]$bounds.Height))"
}

function Close-MainWindow($Window, $Process, [int]$TimeoutSeconds = 15) {
    $Window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    if (-not $Process.WaitForExit($TimeoutSeconds * 1000)) { throw "ECHEC : le logiciel ne s'est pas ferme." }
    Assert-That ($Process.ExitCode -eq 0) "Code de sortie $($Process.ExitCode)"
}

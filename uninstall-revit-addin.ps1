#Requires -Version 5.0
<#
  uninstall-revit-addin.ps1
  ---------------------------
  Removes the "Export to SketchUp" add-in from Revit.
  Searches every Revit Addins folder found on this computer.
#>

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    $msg" -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "    $msg" -ForegroundColor Red }

try {
    Write-Host "======================================================"
    Write-Host "  Revit Add-in Uninstaller -> SketchUp Bridge"
    Write-Host "======================================================"

    Write-Step "Looking for Revit Addins folders on this computer..."

    $addinsRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins"

    if (-not (Test-Path $addinsRoot)) {
        Write-Fail "No Revit Addins folder was found at all ($addinsRoot)."
        Write-Fail "The add-in was likely never installed."
    } else {
        $versionFolders = Get-ChildItem $addinsRoot -Directory -ErrorAction SilentlyContinue

        $found = $false

        foreach ($vf in $versionFolders) {
            $dll   = Join-Path $vf.FullName "RevitToSketchUpExporter.dll"
            $addin = Join-Path $vf.FullName "RevitToSketchUpExporter.addin"

            if ((Test-Path $dll) -or (Test-Path $addin)) {
                $found = $true
                Write-Step "Removing from Revit $($vf.Name)..."
                if (Test-Path $dll)   { Remove-Item $dll -Force;   Write-Ok "Removed: $dll" }
                if (Test-Path $addin) { Remove-Item $addin -Force; Write-Ok "Removed: $addin" }
            }
        }

        Write-Host ""
        if ($found) {
            Write-Host "======================================================"
            Write-Host "  DONE! The add-in has been removed." -ForegroundColor Green
            Write-Host "  Restart Revit so the 'SketchUp Bridge' panel disappears." -ForegroundColor Green
            Write-Host "======================================================"
        } else {
            Write-Fail "No installed add-in was found for any Revit version."
            Write-Fail "It looks like it's already uninstalled, or was never installed."
        }
    }
} catch {
    Write-Host "`n======================================================" -ForegroundColor Red
    Write-Host "  ERROR" -ForegroundColor Red
    Write-Host "======================================================" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    Write-Host ""
    Read-Host "Press Enter to close this window"
}

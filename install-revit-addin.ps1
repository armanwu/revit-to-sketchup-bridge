#Requires -Version 5.0
<#
  install-revit-addin.ps1
  Builds and installs the "Export to SketchUp" Revit add-in.
  Detects the installed Revit version, locates MSBuild, restores/builds the
  project, and copies the output into the matching Revit Addins folder.
#>

$ErrorActionPreference = "Stop"
$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $ScriptDir "RevitAddin"
$Csproj     = Join-Path $ProjectDir "RevitToSketchUpExporter.csproj"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    $msg" -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "    $msg" -ForegroundColor Red }

try {

    Write-Host "======================================================"
    Write-Host "  Revit Add-in Installer -> SketchUp Bridge"
    Write-Host "======================================================"

    Write-Step "Detecting installed Revit versions..."

    $revitInstalls = Get-ChildItem "C:\Program Files\Autodesk" -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match "^Revit \d{4}$" -and (Test-Path (Join-Path $_.FullName "RevitAPI.dll")) }

    if (-not $revitInstalls -or $revitInstalls.Count -eq 0) {
        Write-Fail "No Revit installation found under 'C:\Program Files\Autodesk\'."
        Write-Fail "Make sure Revit is installed, then run this script again."
        throw "Revit not found."
    }

    if ($revitInstalls.Count -eq 1) {
        $chosen = $revitInstalls[0]
    } else {
        Write-Host "    Multiple Revit versions found:"
        for ($i = 0; $i -lt $revitInstalls.Count; $i++) {
            Write-Host "      [$i] $($revitInstalls[$i].Name)"
        }
        $idx = Read-Host "    Enter the number of the version to install into"
        $parsedIdx = 0
        if (-not [int]::TryParse($idx, [ref]$parsedIdx) -or $parsedIdx -lt 0 -or $parsedIdx -ge $revitInstalls.Count) {
            Write-Fail "'$idx' is not a valid choice."
            throw "Invalid Revit version selection."
        }
        $chosen = $revitInstalls[$parsedIdx]
    }

    $revitVersion = ($chosen.Name -replace "Revit ", "")
    Write-Ok "Using Revit $revitVersion ($($chosen.FullName))"

    Write-Step "Looking for MSBuild..."

    $msbuildPath = $null
    $vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"

    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($vsPath) {
            $candidate = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) { $msbuildPath = $candidate }
        }
    }

    if (-not $msbuildPath) {
        $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($dotnet) { $msbuildPath = "dotnet-build" }
    }

    if (-not $msbuildPath) {
        Write-Fail "MSBuild was not found on this computer."
        Write-Fail "Install 'Build Tools for Visual Studio' (free) from:"
        Write-Fail "  https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio"
        Write-Fail "Check the '.NET desktop build tools' workload, then run install.bat again."
        throw "MSBuild not found."
    }

    Write-Ok "MSBuild found: $msbuildPath"

    Write-Step "Restoring the project..."

    if ($msbuildPath -eq "dotnet-build") {
        & dotnet restore $Csproj 2>&1 | ForEach-Object { Write-Host $_ }
    } else {
        & $msbuildPath $Csproj /t:Restore /p:Configuration=Release /p:RevitVersion=$revitVersion /p:Platform=x64 /nologo /verbosity:minimal 2>&1 | ForEach-Object { Write-Host $_ }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Restore failed (exit code $LASTEXITCODE)."
        throw "Restore failed."
    }

    Write-Ok "Restore complete."

    Write-Step "Compiling the add-in for Revit $revitVersion..."

    if ($msbuildPath -eq "dotnet-build") {
        & dotnet build $Csproj -c Release --no-incremental -p:RevitVersion=$revitVersion -p:Platform=x64 2>&1 | ForEach-Object { Write-Host $_ }
    } else {
        & $msbuildPath $Csproj /t:Rebuild /p:Configuration=Release /p:RevitVersion=$revitVersion /p:Platform=x64 /nologo /verbosity:minimal 2>&1 | ForEach-Object { Write-Host $_ }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Build failed (exit code $LASTEXITCODE)."
        throw "Build failed."
    }

    $builtDll = Get-ChildItem -Path $ProjectDir -Recurse -Filter "RevitToSketchUpExporter.dll" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (-not $builtDll) {
        Write-Fail "Build succeeded but the compiled .dll was not found inside $ProjectDir."
        throw "Compiled .dll not found."
    }

    Write-Ok "Build succeeded: $($builtDll.FullName)"

    Write-Step "Installing the add-in into Revit $revitVersion..."

    $addinFolder = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$revitVersion"
    New-Item -ItemType Directory -Path $addinFolder -Force | Out-Null

    Copy-Item $builtDll.FullName -Destination $addinFolder -Force
    Copy-Item (Join-Path $ProjectDir "RevitToSketchUpExporter.addin") -Destination $addinFolder -Force

    Write-Ok "Installed at: $addinFolder"

    Write-Host "`n======================================================"
    Write-Host "  DONE!" -ForegroundColor Green
    Write-Host "  Open (or restart) Revit $revitVersion." -ForegroundColor Green
    Write-Host "  A 'SketchUp Bridge' panel will appear on the Add-Ins tab." -ForegroundColor Green
    Write-Host "======================================================"

} catch {
    Write-Host "`n======================================================" -ForegroundColor Red
    Write-Host "  ERROR" -ForegroundColor Red
    Write-Host "======================================================" -ForegroundColor Red
    Write-Host "Error message: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`nFull details:" -ForegroundColor Red
    Write-Host ($_ | Out-String) -ForegroundColor Red
} finally {
    Write-Host ""
    Read-Host "Press Enter to close this window"
}

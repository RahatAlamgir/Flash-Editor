# Builds both Flash Editor installers into installer\output:
#   FlashEditor-Setup-<version>.exe      Standard: AI models are downloaded on first use
#   FlashEditor-Setup-<version>-AI.exe   AI: both AI models included
#
# Needs: .NET 10 SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup).
# The AI models are taken from a "models" folder (default: the Debug build's models folder).
#
# Usage (from the project folder):
#   powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1
#   powershell -ExecutionPolicy Bypass -File installer\build-installers.ps1 -Version 1.1.0 -ModelsFrom D:\models

param(
    [string]$Version = "1.0.0",
    [string]$ModelsFrom = ""
)

$ErrorActionPreference = "Stop"
$installer = $PSScriptRoot
$root = Split-Path $installer -Parent
$build = Join-Path $installer "build"
if (-not $ModelsFrom) { $ModelsFrom = Join-Path $root "src\FlashEditor.App\bin\Debug\net10.0-windows\models" }

# --- Inno Setup compiler -------------------------------------------------------
$iscc = @(
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }

# --- 1. Publish the app (self-contained: includes the .NET runtime) -------------
Write-Host "Publishing Flash Editor $Version..." -ForegroundColor Cyan
if (Test-Path $build) { Remove-Item $build -Recurse -Force }
dotnet publish "$root\src\FlashEditor.App" -c Release -r win-x64 --self-contained true `
    -p:DebugType=None -p:DebugSymbols=false -p:Version=$Version -o "$build\app"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Get-ChildItem "$build\app" -Filter *.lib | Remove-Item       # linker files, not needed to run

# --- 2. Collect the AI models for the AI edition --------------------------------
$models = "$build\models"
New-Item -ItemType Directory -Force $models | Out-Null
$needed = @("isnet-general-use.onnx", "real_esrgan_x4plus\real_esrgan_x4plus.onnx", "real_esrgan_x4plus\real_esrgan_x4plus.data")
foreach ($file in $needed) {
    $src = Join-Path $ModelsFrom $file
    if (-not (Test-Path $src)) { throw "AI model missing: $src  (run the app once and use the AI tools, or pass -ModelsFrom)" }
    $dst = Join-Path $models $file
    New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
    Copy-Item $src $dst
}

# --- 3. Build both installers ---------------------------------------------------
New-Item -ItemType Directory -Force "$installer\output" | Out-Null
foreach ($withAI in 0, 1) {
    Write-Host "Building installer (AI = $withAI)..." -ForegroundColor Cyan
    & $iscc /Q "/DWithAI=$withAI" "/DAppVersion=$Version" "$installer\FlashEditor.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed (AI = $withAI)" }
}

Get-ChildItem "$installer\output" -Filter *.exe |
    Select-Object Name, @{ n = "Size (MB)"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
Write-Host "Done. Installers are in $installer\output" -ForegroundColor Green

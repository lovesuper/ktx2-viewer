$ErrorActionPreference = "Stop"

Write-Host "Building KTX2 Viewer Release..." -ForegroundColor Cyan

$projectPath = "KtxViewer.UI\KtxViewer.UI\KtxViewer.UI.csproj"
$outputPath = ".\publish"
$ktxDllSource = "C:\Program Files\KTX-Software\bin\ktx.dll"

Write-Host "Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path $outputPath) {
    Remove-Item $outputPath -Recurse -Force
}

Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $outputPath `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Copying ktx.dll..." -ForegroundColor Yellow
if (Test-Path $ktxDllSource) {
    Copy-Item $ktxDllSource $outputPath -Force
    Write-Host "ktx.dll copied successfully" -ForegroundColor Green
} else {
    Write-Host "WARNING: ktx.dll not found at $ktxDllSource" -ForegroundColor Yellow
    Write-Host "Application will not be able to decode BasisU compressed textures!" -ForegroundColor Yellow
}

Write-Host "`nBuild complete!" -ForegroundColor Green
Write-Host "Output: $outputPath\KtxViewer.UI.exe" -ForegroundColor Cyan

$exePath = Join-Path $outputPath "KtxViewer.UI.exe"
if (Test-Path $exePath) {
    $size = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
    Write-Host "Size: $size MB" -ForegroundColor Cyan
}

param(
    [switch]$Installer
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "ApexToolbox\ApexToolbox.csproj"
$portableDir = Join-Path $root "dist\portable"
$liteDir = Join-Path $root "dist\lite"

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $portableDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "发布失败。"
    exit $LASTEXITCODE
}

# 轻量版：不打包 .NET 运行时，需本机安装 .NET 10 桌面运行时
dotnet publish $project -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true `
    -o $liteDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "轻量版发布失败。"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "自包含版已输出到: $portableDir" -ForegroundColor Green
Write-Host "轻量版已输出到: $liteDir" -ForegroundColor Green

if (-not $Installer) { exit 0 }

$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "未找到 Inno Setup 6。安装后再运行本脚本即可生成安装包：" -ForegroundColor Yellow
    Write-Host "https://jrsoftware.org/isdl.php"
    exit 1
}

& $iscc (Join-Path $root "installer.iss")
Write-Host "安装包已输出到: $(Join-Path $root 'dist\installer')" -ForegroundColor Green

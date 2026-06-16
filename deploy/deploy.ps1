#!/usr/bin/env pwsh

# 资产管理系统 - 一键部署脚本（Windows）
# 用法: Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
#      .\deploy.ps1 -Environment Production

param(
    [ValidateSet("Development", "Production")]
    [string]$Environment = "Development",

    [string]$OutputPath = "./app"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  部门资产管理系统 - 部署脚本                           ║" -ForegroundColor Cyan
Write-Host "║  环境: $Environment" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# 1. 检查前置条件
Write-Host "[1/5] 检查前置条件..." -ForegroundColor Yellow

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "✗ .NET SDK 未安装或不在 PATH 中" -ForegroundColor Red
    Write-Host "  请从 https://dotnet.microsoft.com/download/dotnet/8.0 下载安装"
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "✓ .NET 已安装: $dotnetVersion" -ForegroundColor Green

# 2. 清理旧输出
Write-Host "[2/5] 清理旧输出..." -ForegroundColor Yellow

if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force | Out-Null
    Write-Host "✓ 已清理旧目录: $OutputPath" -ForegroundColor Green
}

# 3. 发布后端应用
Write-Host "[3/5] 发布后端应用..." -ForegroundColor Yellow

$backendPath = "./backend"
if (-not (Test-Path "$backendPath/AssetManagement.sln")) {
    Write-Host "✗ 找不到后端项目: $backendPath" -ForegroundColor Red
    exit 1
}

Push-Location $backendPath
try {
    dotnet publish -c Release -o "../$OutputPath" --no-restore
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ 后端发布成功" -ForegroundColor Green
    } else {
        Write-Host "✗ 后端发布失败" -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

# 4. 复制配置文件
Write-Host "[4/5] 复制配置文件..." -ForegroundColor Yellow

$configSource = "./deploy/appsettings.$Environment.json"
$configDest = "$OutputPath/appsettings.Production.json"

if (Test-Path $configSource) {
    Copy-Item $configSource $configDest -Force
    Write-Host "✓ 已复制配置: $configDest" -ForegroundColor Green
} else {
    Write-Host "⚠ 生产配置文件不存在: $configSource，使用默认配置" -ForegroundColor Yellow
}

# 5. 验证部署
Write-Host "[5/5] 验证部署..." -ForegroundColor Yellow

if ((Test-Path "$OutputPath/AssetManagement.Api.exe") -or (Test-Path "$OutputPath/AssetManagement.Api")) {
    Write-Host "✓ 可执行文件存在" -ForegroundColor Green

    $appSize = [math]::Round((Get-ChildItem -Path $OutputPath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
    Write-Host "✓ 应用大小: $($appSize)MB" -ForegroundColor Green
} else {
    Write-Host "✗ 找不到可执行文件" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "部署完成！应用已发布到: $OutputPath" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "后续步骤：" -ForegroundColor Cyan
Write-Host "1. 更新 appsettings.Production.json 中的 JWT Key"
Write-Host "2. 运行应用: cd $OutputPath && .\AssetManagement.Api.exe"
Write-Host "3. 访问 Swagger: http://localhost:5000/swagger"
Write-Host ""

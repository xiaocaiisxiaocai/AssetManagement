param(
    [string]$DbPath = "C:\asset-management\Data\assetmgmt.db",
    [string]$BackupRoot = "\\nas\asset-management-backup",
    [int]$KeepDays = 30
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $DbPath)) {
    throw "SQLite 数据库不存在：$DbPath"
}

if (-not (Test-Path -LiteralPath $BackupRoot)) {
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$name = "assetmgmt_$timestamp.db"
$target = Join-Path $BackupRoot $name

Copy-Item -LiteralPath $DbPath -Destination $target -Force
Write-Host "备份完成：$target"

if ($KeepDays -gt 0) {
    $deadline = (Get-Date).AddDays(-$KeepDays)
    Get-ChildItem -LiteralPath $BackupRoot -Filter "assetmgmt_*.db" |
        Where-Object { $_.LastWriteTime -lt $deadline } |
        Remove-Item -Force
}

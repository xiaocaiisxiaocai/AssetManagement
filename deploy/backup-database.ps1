# SQLite 数据库备份脚本
# 用法: .\backup-database.ps1 或 Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser 后运行

param(
    [string]$DataPath = "Data",
    [string]$BackupPath = "Backups",
    [string]$RetentionDays = 30
)

# 创建备份目录
if (-not (Test-Path $BackupPath)) {
    New-Item -ItemType Directory -Path $BackupPath | Out-Null
    Write-Host "✓ 创建备份目录: $BackupPath"
}

# 生成备份文件名（带时间戳）
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$sourceDb = Join-Path $DataPath "assetmgmt.db"
$backupDb = Join-Path $BackupPath "assetmgmt_$timestamp.db"

# 验证源文件存在
if (-not (Test-Path $sourceDb)) {
    Write-Host "✗ 数据库文件不存在: $sourceDb" -ForegroundColor Red
    exit 1
}

try {
    # 复制数据库文件
    Copy-Item -Path $sourceDb -Destination $backupDb -Force
    Write-Host "✓ 备份成功: $backupDb" -ForegroundColor Green

    # 清理过期备份（超过 RetentionDays 天的文件）
    $cutoffDate = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem -Path $BackupPath -Filter "assetmgmt_*.db" |
        Where-Object { $_.LastWriteTime -lt $cutoffDate } |
        Remove-Item -Force

    Write-Host "✓ 清理过期备份（保留 $RetentionDays 天内的备份）"

    # 显示最近的备份
    $recentBackups = Get-ChildItem -Path $BackupPath -Filter "assetmgmt_*.db" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 5

    Write-Host "`n最近的 5 个备份:"
    $recentBackups | ForEach-Object { Write-Host "  - $($_.Name) ($([math]::Round($_.Length / 1MB, 2))MB)" }
}
catch {
    Write-Host "✗ 备份失败: $_" -ForegroundColor Red
    exit 1
}

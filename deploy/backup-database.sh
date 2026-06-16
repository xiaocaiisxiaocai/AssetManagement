#!/bin/bash

# SQLite 数据库备份脚本（Linux/macOS）
# 用法: chmod +x backup-database.sh && ./backup-database.sh

DATA_PATH="${1:-Data}"
BACKUP_PATH="${2:-Backups}"
RETENTION_DAYS="${3:-30}"

# 创建备份目录
mkdir -p "$BACKUP_PATH"
echo "✓ 备份目录: $BACKUP_PATH"

# 生成备份文件名（带时间戳）
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
SOURCE_DB="$DATA_PATH/assetmgmt.db"
BACKUP_DB="$BACKUP_PATH/assetmgmt_$TIMESTAMP.db"

# 验证源文件存在
if [ ! -f "$SOURCE_DB" ]; then
    echo "✗ 数据库文件不存在: $SOURCE_DB"
    exit 1
fi

# 复制数据库文件
if cp "$SOURCE_DB" "$BACKUP_DB"; then
    echo "✓ 备份成功: $BACKUP_DB"

    # 显示备份大小
    SIZE=$(du -h "$BACKUP_DB" | cut -f1)
    echo "  大小: $SIZE"

    # 清理过期备份（超过 RETENTION_DAYS 天的文件）
    echo "✓ 清理过期备份（保留 $RETENTION_DAYS 天内的备份）"
    find "$BACKUP_PATH" -name "assetmgmt_*.db" -mtime +$RETENTION_DAYS -delete

    # 显示最近的 5 个备份
    echo ""
    echo "最近的 5 个备份:"
    ls -lht "$BACKUP_PATH"/assetmgmt_*.db 2>/dev/null | head -5 | awk '{print "  - " $9 " (" $5 ")"}'
else
    echo "✗ 备份失败: 无法复制文件"
    exit 1
fi

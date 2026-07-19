#!/usr/bin/env bash
set -euo pipefail

echo "此 SQLite 备份脚本已停用。当前系统使用 MySQL，请按 deploy/README-部署.md 的“数据库备份与恢复”章节使用 mysqldump。" >&2
exit 1

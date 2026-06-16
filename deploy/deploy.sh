#!/bin/bash

# 资产管理系统 - 一键部署脚本（Linux）
# 用法: chmod +x deploy.sh && ./deploy.sh production

set -e

ENVIRONMENT="${1:-development}"
OUTPUT_PATH="./app"

echo "╔════════════════════════════════════════════════════════╗"
echo "║  部门资产管理系统 - 部署脚本（Linux）                 ║"
echo "║  环境: $ENVIRONMENT                                      ║"
echo "╚════════════════════════════════════════════════════════╝"
echo ""

# 1. 检查前置条件
echo "[1/5] 检查前置条件..."

if ! command -v dotnet &> /dev/null; then
    echo "✗ .NET SDK 未安装"
    echo "  Ubuntu: sudo apt-get install -y dotnet-sdk-8.0"
    echo "  或从 https://dotnet.microsoft.com/download/dotnet/8.0 下载"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "✓ .NET 已安装: $DOTNET_VERSION"

# 2. 清理旧输出
echo "[2/5] 清理旧输出..."

if [ -d "$OUTPUT_PATH" ]; then
    rm -rf "$OUTPUT_PATH"
    echo "✓ 已清理旧目录: $OUTPUT_PATH"
fi

# 3. 发布后端应用
echo "[3/5] 发布后端应用..."

BACKEND_PATH="./backend"
if [ ! -f "$BACKEND_PATH/AssetManagement.sln" ]; then
    echo "✗ 找不到后端项目: $BACKEND_PATH"
    exit 1
fi

cd "$BACKEND_PATH"
dotnet publish -c Release -o "../$OUTPUT_PATH" --no-restore

if [ $? -eq 0 ]; then
    echo "✓ 后端发布成功"
else
    echo "✗ 后端发布失败"
    cd ..
    exit 1
fi
cd ..

# 4. 复制配置文件
echo "[4/5] 复制配置文件..."

CONFIG_SOURCE="./deploy/appsettings.${ENVIRONMENT}.json"
CONFIG_DEST="$OUTPUT_PATH/appsettings.Production.json"

if [ -f "$CONFIG_SOURCE" ]; then
    cp "$CONFIG_SOURCE" "$CONFIG_DEST"
    echo "✓ 已复制配置: $CONFIG_DEST"
else
    echo "⚠ 配置文件不存在: $CONFIG_SOURCE，使用默认配置"
fi

# 5. 验证部署
echo "[5/5] 验证部署..."

if [ -f "$OUTPUT_PATH/AssetManagement.Api" ] || [ -f "$OUTPUT_PATH/AssetManagement.Api.exe" ]; then
    echo "✓ 可执行文件存在"

    APP_SIZE=$(du -sh "$OUTPUT_PATH" | awk '{print $1}')
    echo "✓ 应用大小: $APP_SIZE"
else
    echo "✗ 找不到可执行文件"
    exit 1
fi

echo ""
echo "═══════════════════════════════════════════════════════"
echo "部署完成！应用已发布到: $OUTPUT_PATH"
echo "═══════════════════════════════════════════════════════"
echo ""
echo "后续步骤："
echo "1. 更新 appsettings.Production.json 中的 JWT Key"
echo "2. 运行应用: cd $OUTPUT_PATH && ./AssetManagement.Api"
echo "3. 访问 Swagger: http://localhost:5000/swagger"
echo ""

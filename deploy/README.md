# 部门资产管理系统 — 生产部署指南

**版本**：v1.0 | **日期**：2026-06-16

---

## 1. 系统环境要求

### 最低配置
- **操作系统**：Windows Server 2019+ 或 Linux (Ubuntu 20.04+)
- **运行时**：.NET 8 Runtime
- **数据库**：SQLite 3.30+（已集成，无需独立安装）
- **内存**：2GB (建议 4GB)
- **磁盘**：500MB + 数据库大小（预估 100MB～500MB）
- **网络**：内网 LAN，10/100 Mbps 以上

### 软件依赖
- **.NET 8 Runtime**：从 https://dotnet.microsoft.com/download/dotnet/8.0 下载并安装
- **Node.js 18+**（仅前端开发/构建时需要）

---

## 2. 前端部署

### 2.1 构建前端应用

```bash
cd web
pnpm install
pnpm -F @vben/web-ele run build
# 生成输出目录：web/apps/web-ele/dist/
```

### 2.2 托管前端静态文件

将 `web/apps/web-ele/dist/` 中的所有文件复制到以下位置之一：

#### 选项 A：IIS 托管（Windows 推荐）
1. 在 IIS 中创建新网站，物理路径指向 `dist/` 目录
2. 配置绑定：主机名 `assetmgmt.internal.local`（或内网 IP），端口 80 或 443（HTTPS）
3. 添加 URL Rewrite 规则（支持 SPA 路由）：

```xml
<rewrite>
  <rules>
    <rule name="React Router" stopProcessing="true">
      <match url="^(?!api|swagger).*" />
      <conditions>
        <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
        <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
      </conditions>
      <action type="Rewrite" url="index.html" />
    </rule>
  </rules>
</rewrite>
```

#### 选项 B：Nginx 托管（Linux）
```nginx
server {
    listen 80;
    server_name assetmgmt.internal.local;

    root /var/www/assetmgmt/dist;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### 2.3 环境配置

创建 `.env.production`（如需动态覆盖）：

```env
# .env.production
VITE_API_BASE_URL=http://assetmgmt.internal.local/api
VITE_APP_TITLE=部门资产管理系统
```

---

## 3. 后端部署

### 3.1 准备环境

#### Windows
```powershell
# 下载并安装 .NET 8 Runtime
# 验证安装
dotnet --version
# 应输出 8.x.x
```

#### Linux
```bash
sudo apt-get update
sudo apt-get install -y dotnet-runtime-8.0
dotnet --version
```

### 3.2 配置应用设置

编辑 `backend/src/AssetManagement.Api/appsettings.Production.json`：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "AllowedHosts": "assetmgmt.internal.local,192.168.x.x",
  "ConnectionStrings": {
    "Default": "Data Source=Data/assetmgmt.db"
  },
  "Jwt": {
    "Key": "<REPLACE_WITH_PRODUCTION_KEY>",
    "Issuer": "AssetManagement",
    "ExpireMinutes": 120
  }
}
```

**⚠️ 关键**：
- `Jwt.Key` 必须替换为 32+ 字符的随机密钥（参见 `backup-database.ps1` 中的生成方法）
- 不要在版本控制中提交生产密钥

### 3.3 发布应用

#### Windows
```powershell
cd backend
dotnet publish -c Release -o ../deploy/app
```

#### Linux
```bash
cd backend
dotnet publish -c Release -o ../deploy/app
```

输出目录结构：
```
deploy/app/
├── AssetManagement.Api.exe / AssetManagement.Api (可执行文件)
├── appsettings.json
├── appsettings.Production.json
├── Data/ (数据库目录)
└── wwwroot/ (Swagger UI 静态文件)
```

### 3.4 创建数据库

首次运行时，应用会自动创建并初始化 SQLite 数据库：

```bash
cd deploy/app
# Windows
.\AssetManagement.Api.exe --environment Production

# Linux
./AssetManagement.Api --environment Production
```

启动日志示例：
```
info: Microsoft.EntityFrameworkCore.Migrations[20405]
      Applying migration '20260616082752_AddConfirmedAtToApprovalFlow'.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

按 Ctrl+C 停止应用。

### 3.5 设置服务

#### Windows：创建 Windows 服务

使用 NSSM (Non-Sucking Service Manager)：

```powershell
# 下载 nssm: https://nssm.cc/download
# 解压后运行：
nssm install AssetManagementAPI "D:\deploy\app\AssetManagement.Api.exe" "--environment Production"
nssm set AssetManagementAPI AppDirectory D:\deploy\app
nssm set AssetManagementAPI AppEnvironmentExtra ASPNETCORE_URLS=http://+:5000
nssm set AssetManagementAPI AppStdout D:\deploy\logs\api.log
nssm set AssetManagementAPI AppStderr D:\deploy\logs\api-error.log

# 启动服务
nssm start AssetManagementAPI
```

#### Linux：创建 systemd 服务

创建 `/etc/systemd/system/assetmgmt-api.service`：

```ini
[Unit]
Description=Asset Management API
After=network.target

[Service]
Type=notify
User=assetmgmt
WorkingDirectory=/opt/assetmgmt/app
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="ASPNETCORE_URLS=http://+:5000"
ExecStart=/opt/assetmgmt/app/AssetManagement.Api
Restart=on-failure
RestartSec=10

[Install]
WantedBy=multi-user.target
```

启动服务：
```bash
sudo systemctl daemon-reload
sudo systemctl enable assetmgmt-api
sudo systemctl start assetmgmt-api
sudo systemctl status assetmgmt-api
```

### 3.6 验证部署

```bash
# 检查健康状态
curl http://localhost:5000/api/health
# 预期响应: {"code":0,"message":"ok","data":"healthy"}

# 查看 Swagger 文档
# 访问：http://localhost:5000/swagger/index.html
```

---

## 4. 数据库备份

### 4.1 自动备份（推荐）

#### Windows
```powershell
# 编辑任务计划程序
# 创建任务：每天 02:00 运行
# 操作：C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe
# 参数：-ExecutionPolicy Bypass -File "D:\deploy\backup-database.ps1"
```

#### Linux
```bash
# 添加到 crontab
crontab -e

# 每天凌晨 2 点执行备份
0 2 * * * /opt/assetmgmt/deploy/backup-database.sh
```

### 4.2 手动备份

```bash
# Windows
.\backup-database.ps1 -DataPath "deploy/app/Data" -BackupPath "deploy/Backups"

# Linux
bash backup-database.sh deploy/app/Data deploy/Backups 30
```

### 4.3 恢复数据库

```bash
# 1. 停止应用
# Windows: nssm stop AssetManagementAPI
# Linux: sudo systemctl stop assetmgmt-api

# 2. 恢复备份
cp deploy/Backups/assetmgmt_20260616_120000.db deploy/app/Data/assetmgmt.db

# 3. 启动应用
# Windows: nssm start AssetManagementAPI
# Linux: sudo systemctl start assetmgmt-api
```

---

## 5. 访问应用

启动后，使用以下地址访问：

| 组件 | 地址 | 说明 |
|------|------|------|
| 前端应用 | `http://assetmgmt.internal.local` 或 `http://192.168.x.x` | 登录后访问 |
| 后端 API | `http://assetmgmt.internal.local/api` | RESTful API 前缀 |
| API 文档 | `http://localhost:5000/swagger/index.html` | Swagger UI（仅开发/运维） |

**默认账号**：
- 工号：`1001`
- 密码：`123456`（**请在首次登录后立即修改**）

---

## 6. 故障排查

### 应用无法启动

```bash
# Windows
# 查看事件日志
Get-EventLog -LogName Application -Source ".NET Runtime" -Newest 10

# Linux
# 查看日志
journalctl -u assetmgmt-api -n 20
```

### 数据库锁定

如果出现 "database is locked" 错误：
1. 确认只有一个应用实例在运行
2. 重启应用
3. 检查是否有其他 SQLite 客户端打开了数据库文件

### 性能问题

- 监控进程 CPU 和内存使用（Windows 任务管理器 / Linux `top`）
- 检查网络连接和内网带宽
- 考虑启用应用程序缓存（如有配置选项）

---

## 7. 维护

### 定期检查

- **每周**：查看审计日志，确认无异常操作
- **每月**：验证备份完整性，测试恢复流程
- **每半年**：检查 .NET Runtime 更新，评估系统性能

### 日志位置

- **Windows**：`Event Viewer → Windows Logs → Application`
- **Linux**：`/var/log/syslog` 或 `journalctl -u assetmgmt-api`

---

## 8. 生产检查清单

部署前，确认以下项：

- [ ] .NET 8 Runtime 已安装
- [ ] 前端构建成功（`dist/` 目录存在）
- [ ] 后端发布成功（Release 二进制文件）
- [ ] JWT 密钥已生成并配置
- [ ] 数据库备份脚本测试通过
- [ ] 访问权限配置正确（防火墙规则）
- [ ] 默认管理员账号密码已修改
- [ ] 健康检查接口可访问
- [ ] 前端可正常加载，能够登录
- [ ] 审批流程和权限路由正常工作
- [ ] 备份计划已设置

---

## 附录：常用命令

```bash
# 查看 .NET 版本
dotnet --version

# 发布为独立可执行文件（无需 .NET Runtime）
dotnet publish -c Release --self-contained -r win-x64 -o ../deploy/app

# 查看应用配置
cat backend/src/AssetManagement.Api/appsettings.Production.json

# 启用 HTTPS（需要证书）
# 修改 appsettings.Production.json 中的 ASPNETCORE_URLS
```

---

**支持**：如有问题，查看 `docs/` 目录中的设计文档或联系系统管理员。

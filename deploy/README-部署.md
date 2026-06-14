# 部门资产管理系统部署说明

适用场景：Windows Server 内网单机部署，后端 .NET 8 + SQLite，前端静态文件同源或独立站点托管。

## 1. 环境要求

- Windows Server 2019/2022 或等价内网 Windows 主机。
- .NET 8 Runtime；若使用 IIS，安装 ASP.NET Core Hosting Bundle。
- Node.js 20+ 与 pnpm，仅构建前端时需要。
- 部署目录示例：`C:\asset-management`。

## 2. 后端发布

在仓库根目录执行：

```powershell
dotnet publish backend/src/AssetManagement.Api -c Release -o deploy/api --self-contained false
```

复制 `deploy/appsettings.Production.json` 到发布目录 `deploy/api/appsettings.Production.json`，并至少修改：

- `Jwt:Key`：替换为 32 位以上随机字符串。
- `ConnectionStrings:Default`：默认 `Data Source=Data/assetmgmt.db`，表示数据库在发布目录下 `Data` 文件夹。

首次启动前创建数据目录：

```powershell
New-Item -ItemType Directory -Force C:\asset-management\Data
```

程序启动时会自动执行 EF Core `Migrate()` 并写入种子数据，包括管理员、角色权限、菜单、系统参数和默认审批流。

默认账号：`1001 / 123456`。首次登录后应修改密码。

## 3. 运行方式

### 方式 A：Kestrel

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"
dotnet C:\asset-management\AssetManagement.Api.dll --urls http://0.0.0.0:8080
```

建议用 NSSM 或 Windows 服务托管该命令，服务账号需要对部署目录和 `Data` 目录有读写权限。

### 方式 B：IIS

1. 安装 ASP.NET Core Hosting Bundle。
2. 新建站点，物理路径指向后端发布目录。
3. 应用程序池设置为“无托管代码”。
4. 设置环境变量 `ASPNETCORE_ENVIRONMENT=Production`。

启动后检查：

- `http://服务器IP:端口/api/health`
- 开发期可用 `/swagger`；生产环境默认不启用 Swagger。

## 4. 前端构建与托管

生产前端接口前缀为 `/api`，适合同源部署。

```powershell
cd web
pnpm install
pnpm --filter @vben/web-ele... run build
```

构建产物在 `web/apps/web-ele/dist`，`dist.zip` 为脚手架自动生成的压缩包，不提交到仓库。

托管推荐二选一：

- 同源托管：将 `web/apps/web-ele/dist` 内文件复制到后端发布目录的 `wwwroot`，由 ASP.NET Core 或 IIS 静态文件托管。
- 独立站点：IIS/Nginx 单独托管 `dist`，将 `/api` 反向代理到后端。

当前前端使用 hash 路由，独立静态托管不需要额外 history fallback。

## 5. SQLite 备份与恢复

备份脚本：`deploy/backup.ps1`。

示例：

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\backup.ps1 `
  -DbPath "C:\asset-management\Data\assetmgmt.db" `
  -BackupRoot "\\nas\asset-management-backup" `
  -KeepDays 30
```

可用 Windows 计划任务定期执行脚本。该定时任务属于运维层文件备份，不是应用内定时任务。

恢复步骤：

1. 停止后端服务。
2. 备份当前 `assetmgmt.db`。
3. 将目标备份文件复制回 `Data\assetmgmt.db`。
4. 启动后端服务并访问 `/api/health`。

## 6. 常见问题

- 登录后菜单缺失：确认数据库种子已执行，管理员角色包含菜单权限；已有库升级时重启后端会补增量菜单。
- 前端接口 404：确认生产环境 `VITE_GLOB_API_URL=/api`，且反代或同源路径正确。
- 数据库无法写入：确认服务账号对部署目录和 `Data` 目录有写权限。
- 端口无法访问：检查 Windows 防火墙和 IIS/Kestrel 监听地址。

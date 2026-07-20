# 部门资产管理系统部署说明

适用场景：Windows Server 内网单机部署，后端 .NET 8 + MySQL 5.7，前端静态文件同源或独立站点托管。

## 1. 环境要求

- Windows Server 2019/2022 或等价内网 Windows 主机。
- .NET 8 Runtime；若使用 IIS，安装 ASP.NET Core Hosting Bundle。
- **MySQL 5.7+**（独立安装，程序不内嵌）：需提前创建数据库 `assetmgmt`，并授予应用账号读写权限。
- Node.js 20+ 与 pnpm，仅构建前端时需要。
- 部署目录示例：`C:\asset-management`。

## 2. 后端发布

在仓库根目录执行：

```powershell
dotnet publish backend/src/AssetManagement.Api -c Release -o deploy/api --self-contained false
```

复制 `deploy/appsettings.Production.json` 到发布目录 `deploy/api/appsettings.Production.json`，并至少修改：

- `Jwt:Key`：替换为 32 位以上随机字符串。
- `ConnectionStrings:Default`：替换 `REPLACE_MYSQL_HOST`、`REPLACE_MYSQL_USER`、`REPLACE_MYSQL_PASSWORD` 为实际 MySQL 连接信息。
  示例：`Server=localhost;Port=3306;Database=assetmgmt;User=assetmgmt_user;Password=YourStrongPassword;CharSet=utf8mb4;`
- `Attachment:Path` 与 `DatabaseBackup:Path`：必须替换占位值，使用两个彼此独立、互不包含的绝对目录；禁止把备份放进附件目录或 Web 静态目录。

生产环境发现任一 `REPLACE_` 配置占位符会拒绝启动，不要把模板文件原样投入生产。

更推荐由服务管理器或部署平台注入敏感配置，避免把真实凭据写入发布目录：

```powershell
$env:ConnectionStrings__Default = '<MySQL 连接字符串>'
$env:Jwt__Key = '<至少 32 字符的随机密钥>'
$env:ASSET_ADMIN_PASSWORD = '<首次初始化管理员的强密码>'
```

提前在 MySQL 中创建数据库与用户：

```sql
CREATE DATABASE IF NOT EXISTS assetmgmt CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'assetmgmt_user'@'localhost' IDENTIFIED BY 'YourStrongPassword';
GRANT ALL PRIVILEGES ON assetmgmt.* TO 'assetmgmt_user'@'localhost';
FLUSH PRIVILEGES;
```

程序启动默认不会自动改库。首次部署或升级需要自动执行 EF Core `Migrate()`、补齐管理员、角色权限、菜单、系统参数和默认审批流时，在配置中显式开启一次：

```json
"Database": {
  "AutoMigrate": true,
  "AutoSeed": true
}
```

确认迁移和种子完成后，生产环境建议改回 `false`。

### 升级前的数据完整性检查

`20260719112523_HardenTestProjectAndMaterialIntegrity` 迁移会为项目编号/名称、活动料件名称增加唯一约束，并为料件和项目跟进增加项目外键。迁移会在执行任何 MySQL DDL 前检查历史重复数据和孤儿数据；若报错包含 `migration blocked:`，应先备份数据库，按错误中的 `duplicate`/`orphan` 类型人工核对并修复数据，然后重新执行迁移。预检失败时不会部分创建业务索引、外键或序列表。

该预检会临时创建并删除存储过程，执行迁移的账号需具有 `CREATE ROUTINE`/`ALTER ROUTINE` 权限；本文上述 `GRANT ALL PRIVILEGES ON assetmgmt.*` 方案已包含所需权限。

初始化管理员工号为 `1001`。生产环境必须设置 `ASSET_ADMIN_PASSWORD`；未设置时的回退密码 `123456` 仅供本地开发，使用默认密码登录后必须先修改密码。

## 3. 运行方式

### 方式 A：Kestrel

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"
dotnet C:\asset-management\AssetManagement.Api.dll --urls http://127.0.0.1:8080
```

建议用 NSSM 或 Windows 服务托管该命令，服务账号需要对附件和备份目录有读写权限。Kestrel 仅监听本机回环地址，由 IIS/Nginx/Caddy 终止 TLS 并反向代理；不得把明文 HTTP 端口直接暴露给内网客户端。

### 方式 B：IIS

1. 安装 ASP.NET Core Hosting Bundle。
2. 新建站点，物理路径指向后端发布目录。
3. 应用程序池设置为“无托管代码”。
4. 设置环境变量 `ASPNETCORE_ENVIRONMENT=Production`。
5. 为站点绑定有效的内网 HTTPS 证书并启用 HTTP→HTTPS 重定向；反向代理必须传递 `X-Forwarded-Proto`，且只信任 `ForwardedHeaders:KnownProxies` 中列出的代理地址。

启动后检查：

- `https://服务器域名/api/health/live`：仅检查进程存活。
- `https://服务器域名/api/health` 或 `/api/health/ready`：检查 API 与 MySQL 均可用；数据库不可连接时返回 HTTP 503。
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

- IIS 同源托管：将 `web/apps/web-ele/dist` 内文件复制到 IIS 站点可访问的静态目录，并由 IIS 托管；当前 API 未启用 ASP.NET Core 静态文件中间件，不能仅复制到 `wwwroot` 后直接由 Kestrel 提供前端页面。
- 独立站点：IIS/Nginx 单独托管 `dist`，将 `/api` 反向代理到后端。

当前前端使用 hash 路由，独立静态托管不需要额外 history fallback。

## 5. 数据库备份与恢复

使用 MySQL 自带的 `mysqldump` 工具备份：

```powershell
mysqldump -u assetmgmt_user -p assetmgmt > "\\nas\backup\assetmgmt_$(Get-Date -Format 'yyyyMMdd_HHmmss').sql"
```

应用内“数据库备份”会生成 ZIP 完整包，其中 `database/` 是 MySQL 导出，`attachments/` 是上传附件快照。也可用 Windows 计划任务执行独立 SQL 备份。恢复完整包步骤：

1. 停止后端服务。
2. 将 ZIP 解压到临时目录，先核对包内仅含预期的 `database/` 与 `attachments/` 内容。
3. 恢复数据库：`mysql -u assetmgmt_user -p assetmgmt < database\backup_file.sql`。
4. 清空目标附件目录后，把包内 `attachments/` 的内容复制到当前 `Attachment:Path`；保持原文件名，不要把 ZIP 本身放入附件目录。
5. 确认 `DatabaseBackup:Path` 与 `Attachment:Path` 互不包含，且服务账号权限正确。
6. 启动后端并访问 `/api/health/ready`；抽查资产和测试料件图片可正常打开。

恢复会覆盖业务状态，应先保留当前数据库和附件目录的独立快照，并在维护窗口操作。

> 旧版 SQLite 备份脚本（`backup.ps1`、`backup-database.*`）已停用，执行时会直接报错，不会再复制或删除文件。

> 旧版 `deploy.ps1`/`deploy.sh` 只发布后端且会递归清空可配置目录，现已停用。请严格按本文第 2、4 节分别发布后端与正式 `web-ele` 前端。

## 6. 常见问题

- 登录后菜单缺失：确认数据库种子已执行，管理员角色包含菜单权限；已有库升级时重启后端会补增量菜单。
- 前端接口 404：确认生产环境 `VITE_GLOB_API_URL=/api`，且反代或同源路径正确。
- 数据库连接失败：确认 MySQL 已启动、连接字符串正确、防火墙允许 3306 端口、数据库账号权限已授予。
- 端口无法访问：检查 Windows 防火墙和 IIS/Kestrel 监听地址。

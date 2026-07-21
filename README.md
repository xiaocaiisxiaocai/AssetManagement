# 部门资产管理系统

本仓库包含部门资产管理系统的设计文档、静态原型和全栈实现。

## 目录

- `docs/`：需求、设计、实施规划和里程碑计划。
- `prototype/`：早期静态原型。
- `backend/`：ASP.NET Core 8 + EF Core + MySQL 5.7 后端。
- `web/`：Vue 3 / Vben / Element Plus 前端。
- `deploy/`：内网部署说明、生产配置样例和 MySQL/附件完整备份方案。

## 本地运行

后端：

```powershell
$env:ConnectionStrings__Default = 'Server=localhost;Port=3306;Database=assetmgmt;User=<本地用户>;Password=<本地密码>;CharSet=utf8mb4;'
$env:Jwt__Key = '<至少 32 字符的本地随机密钥>'
# 仅首次初始化可选；生产环境必须设置强密码
$env:ASSET_ADMIN_PASSWORD = '<管理员初始密码>'
dotnet run --project backend/src/AssetManagement.Api
```

健康检查：`http://localhost:5292/api/health` 或按控制台输出端口访问。

前端：

```powershell
cd web
pnpm install
pnpm -F @vben/web-ele dev
```

首次初始化会创建管理员工号 `1001`。生产环境必须通过
`ASSET_ADMIN_PASSWORD` 提供强初始密码；未配置时的 `123456` 仅供本地开发，
登录后会被强制要求修改且不能访问其他业务接口。

## 验证命令

```powershell
dotnet build .\backend\AssetManagement.sln
dotnet test .\backend\tests\AssetManagement.Tests --no-build
cd web
pnpm -F @vben/web-ele run typecheck
pnpm --filter @vben/web-ele... run build
```

## 部署

部署方案见 `deploy/README-部署.md`。

升级已有数据库时，首次启动需显式开启一次
`Database:AutoMigrate=true` 和 `Database:AutoSeed=true`，用于应用迁移并增量补齐
权限、菜单、系统参数和工作流基础数据；确认完成后应关闭自动迁移/种子。

总体实施路线见 `docs/全栈实施规划.md`，分阶段计划见 `docs/plans/`。

## 注意

- 不提交真实员工数据、生产密钥、内网地址。
- 不提交数据库文件/备份、前端 `dist/`、`dist.zip` 和日志文件。
- 生产环境必须替换 `deploy/appsettings.Production.json` 中的 `Jwt:Key`。
